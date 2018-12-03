using GzipStreamExtensions.GZipTest.Facilities;
using GzipStreamExtensions.GZipTest.Services.Abstract;
using GzipStreamExtensions.GZipTest.Threads;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileOperationsManager : IFileOperationsManager
    {
        private readonly IThreadStateDispatcher threadStateDispatcher;
        private readonly ILog log;

        public FileOperationsManager(IThreadStateDispatcher threadStateDispatcher, ILog log)
        {
            this.threadStateDispatcher = threadStateDispatcher;
            this.log = log;
        }

        public ResponseContainer RunByFileTaskDescriptor(FileTaskDescriptor fileTaskDescriptor)
        {
            var result = new ResponseContainer(success: true);

            try
            {
                log.LogInfo($"Source file size is {fileTaskDescriptor.FileLength} bytes.");

                var stopWatch = Stopwatch.StartNew();
                var globalQueueResponseContainer = GetGlobalQueue(fileTaskDescriptor);

                result.Join(globalQueueResponseContainer);

                if (!result.Success)
                    return result;

                var threadTask = GetThreadTask(globalQueueResponseContainer.Value, fileTaskDescriptor);
                var enqueueResult = threadStateDispatcher.EnqueueTask(threadTask);
                threadTask.State.EnqueueResult = enqueueResult;
                threadTask.State.IsAwaitable = enqueueResult.ThreadStates.Count() > 1;

                threadStateDispatcher.StartTask(enqueueResult);

                log.LogInfo("Please wait...");

                threadStateDispatcher.WaitTaskCompleted();
                stopWatch.Stop();

                result.Join(threadTask.State.ResponseContainer);
                log.LogInfo($"Completed! Elapsed in {stopWatch.Elapsed}.");
            }
            catch(Exception ex)
            {
                result.AddErrorMessage($"Error while running file descriptor task. Message: {ex.Message}");
            }

            return result;
        }

        private ResponseContainer<Queue<InternalGlobalQueueTask>> GetGlobalQueue(FileTaskDescriptor fileTaskDescriptor)
        {
            var result = new ResponseContainer<Queue<InternalGlobalQueueTask>>(success: true);
            var fileLength = fileTaskDescriptor.FileLength;

            if (fileLength == 0)
            {
                result.AddErrorMessage($"File {fileTaskDescriptor.SourceFilePath} is empty. Nothing to compress.");
                return result;
            }

            var readBufferSize = fileTaskDescriptor.FileOperationStrategyParameters.ReadBufferSize;
            var count = (long)Math.Ceiling(fileLength / (double)readBufferSize);

            if (count > (int.MaxValue / 100))
            {
                result.AddErrorMessage($"Read buffer size {readBufferSize} is not supported.");
                return result;
            }

            var queue = new Queue<InternalGlobalQueueTask>();

            for (var i = 0; i < count; i++)
            {
                var internalTask = new InternalGlobalQueueTask
                {
                    SeekPoint = readBufferSize * i,
                    Order = i
                };
                queue.Enqueue(internalTask);
            }

            result.SetSuccessValue(queue);
            return result;
        }

        private ThreadTask<State> GetThreadTask(Queue<InternalGlobalQueueTask> queue, FileTaskDescriptor fileTaskDescriptor)
        {
            var result = new ThreadTask<State>
            {
                Action = Work,
                State = new State
                {
                    GlobalQueue = queue,
                    FileTaskDescriptor = fileTaskDescriptor
                }
            };
            return result;
        }

        private void Work(State state)
        {
            try
            {
                while (true)
                {
                    if (state.IsDone)
                        return;

                    if (!WorkWithGlobalQueue(state))
                        return;

                    InternalLocalQueueTask localQueueTask = null;

                    if (!WorkWithLocalQueue(state, out localQueueTask))
                        continue;
                    
                    WorkWithFlushBuffer(state, localQueueTask);
                }
            }
            catch(Exception ex)
            {
                state.IsDone = true;
                state.ResponseContainer.AddErrorMessage($"Error while performing background work. Message: {ex.Message}{Environment.NewLine}{ex.StackTrace}.");
            }
        }

        private bool WorkWithGlobalQueue(State state)
        {
            var result = true;

            if (!state.IsLocalQueueEmpty)
                return result;

            state.LockGlobalQueue();

            if (!state.IsLocalQueueEmpty || state.IsDone)
            {
                state.UnlockGlobalQueue();
                return result;
            }

            var globalQueueTask = state.GlobalQueue.Any() ? state.GlobalQueue.Dequeue() : null;

            if (globalQueueTask == null)
            {
                state.IsDone = true;
                state.UnlockGlobalQueue();
                return false;
            }

            state.CurrentGlobalQueueTask = globalQueueTask;

            if (state.FlushGlobalQueueTask == null)
                state.FlushGlobalQueueTask = state.CurrentGlobalQueueTask;

            var strategyParameters = state.FileTaskDescriptor.FileOperationStrategyParameters;
            state.ReadBufferSize = globalQueueTask.SeekPoint + strategyParameters.ReadBufferSize < state.FileTaskDescriptor.FileLength
                ? strategyParameters.ReadBufferSize
                : checked((int)(state.FileTaskDescriptor.FileLength - globalQueueTask.SeekPoint));
            state.ReadBuffer = new byte[state.ReadBufferSize];
            state.LocalQueue = GetLocalQueue(state);
            state.CurrentGlobalQueueTask.TasksCount = state.LocalQueue.Count;
            state.IsLocalQueueEmpty = !state.LocalQueue.Any();

            log.LogInfo($"Working with range of bytes {state.CurrentGlobalQueueTask.SeekPoint} - {state.CurrentGlobalQueueTask.SeekPoint + state.ReadBufferSize}.");

            state.UnlockGlobalQueue();

            return result;
        }

        private Queue<InternalLocalQueueTask> GetLocalQueue(State state)
        {
            var result = new Queue<InternalLocalQueueTask>();
            var strategyParameters = state.FileTaskDescriptor.FileOperationStrategyParameters;
            var localBufferSize = (int)Math.Ceiling((double)strategyParameters.ReadBufferSize / strategyParameters.ReadBufferChunks);
            var tasksCount = state.FileTaskDescriptor.FileOperationStrategyParameters.ReadBufferChunks;
            var offset = 0;

            for (int i = 0; i < tasksCount; i++)
            {
                var tempBufferSize = localBufferSize;
                var tempPosition = offset + localBufferSize;

                if (tempPosition > state.ReadBufferSize)
                    tempBufferSize = state.ReadBufferSize - offset;

                var task = new InternalLocalQueueTask(state.CurrentGlobalQueueTask)
                {
                    ReadBuffer = state.ReadBuffer,
                    ReadBufferOffset = offset,
                    ReadBufferSize = tempBufferSize,
                    Order = i
                };
                result.Enqueue(task);

                offset += tempBufferSize;
            }
            return result;
        }

        private bool WorkWithLocalQueue(State state, out InternalLocalQueueTask localQueueTask)
        {
            var result = true;
            localQueueTask = null;

            if (state.IsLocalQueueEmpty)
                return false;

            state.LockLocalQueue();

            if (state.IsLocalQueueEmpty)
            {
                state.UnlockLocalQueue();
                return false;
            }

            localQueueTask = state.LocalQueue.Dequeue();

            if (!state.LocalQueue.Any())
                state.IsLocalQueueEmpty = true;

            var strategy = state.FileTaskDescriptor.FileOperationStrategy;
            var strategyParameters = state.FileTaskDescriptor.FileOperationStrategyParameters;
            var mutableStrategyParameters = new FileOperationStrategyMutableParameters
            {
                Buffer = localQueueTask.ReadBuffer,
                Offset = localQueueTask.ReadBufferOffset,
                BufferSize = localQueueTask.ReadBufferSize
            };
            strategy.Read(strategyParameters, mutableStrategyParameters);
            localQueueTask.StrategyMutableParameters = mutableStrategyParameters;

            state.UnlockLocalQueue();

            return result;
        }

        private void WorkWithFlushBuffer(State state, InternalLocalQueueTask localQueueTask)
        {
            if (state.IsAwaitable && localQueueTask.GlobalQueueTask.Order > state.FlushGlobalQueueTask.Order)
            {
                state.WaitFlushBufferWhile(() => localQueueTask.GlobalQueueTask.Order > state.FlushGlobalQueueTask.Order);
            }

            state.LockFlushBuffer();

            var strategy = state.FileTaskDescriptor.FileOperationStrategy;
            var strategyParameters = state.FileTaskDescriptor.FileOperationStrategyParameters;
            var mutableStrategyParameters = new FileOperationStrategyMutableParameters
            {
                Buffer = localQueueTask.StrategyMutableParameters.Buffer,
                Offset = localQueueTask.StrategyMutableParameters.Offset,
                BufferSize = localQueueTask.StrategyMutableParameters.BytesRead,
                IsCompleted = localQueueTask.StrategyMutableParameters.IsCompleted
            };

            strategy.Write(strategyParameters, mutableStrategyParameters);
            
            state.FlushGlobalQueueTask = state.CurrentGlobalQueueTask;

            state.UnlockFlushBuffer();
        }

        private class InternalGlobalQueueTask
        {
            public long SeekPoint { get; set; }
            public int TasksCount { get; set; }
            public int Order { get; set; }
        }

        private class InternalLocalQueueTask
        {
            public int ReadBufferOffset { get; set; }
            public int ReadBufferSize { get; set; }
            public byte[] ReadBuffer { get; set; }
            public byte[] WriteBuffer { get; set; }
            public int Order { get; set; }
            public InternalGlobalQueueTask GlobalQueueTask { get; set; }
            public FileOperationStrategyMutableParameters StrategyMutableParameters { get; set; }

            public InternalLocalQueueTask(InternalGlobalQueueTask globalQueueTask)
            {
                ReadBuffer = new byte[0];
                WriteBuffer = new byte[0];
                GlobalQueueTask = globalQueueTask;
            }
        }

        private class State
        {
            private readonly object globalQueueSynchronization = new object();
            private readonly object localQueueSynchronization = new object();
            private readonly object flushBufferSynchronization = new object();
            private volatile bool isLocalQueueEmpty;
            private volatile bool isDone;
            private volatile InternalGlobalQueueTask flushGlobalQueueTask;

            public ThreadStateDispatcherEnqueueResult<State> EnqueueResult { get; set; }
            public Queue<InternalGlobalQueueTask> GlobalQueue { get; set; }
            public Queue<InternalLocalQueueTask> LocalQueue { get; set; }
            public FileTaskDescriptor FileTaskDescriptor { get; set; }
            public byte[] ReadBuffer { get; set; }
            public int ReadBufferSize { get; set; }
            public InternalGlobalQueueTask CurrentGlobalQueueTask { get; set; }
            public InternalGlobalQueueTask FlushGlobalQueueTask
            {
                get { return flushGlobalQueueTask;  }
                set { flushGlobalQueueTask = value; }
            }
            public int TotalBytesCountWroteToReadBuffer { get; set; }
            public int TotalBytesCountWroteToFlushBuffer { get; set; }
            public List<InternalLocalQueueTask> UnorderedCompletedLocalTasksCollection { get; set; }
            public ResponseContainer ResponseContainer { get; set; }
            public bool IsDone
            {
                get { return isDone; }
                set { isDone = value; }
            }
            public bool IsLocalQueueEmpty
            {
                get { return isLocalQueueEmpty; }
                set { isLocalQueueEmpty = value; }
            }
            public bool IsAwaitable { get; set; }

            public State()
            {
                UnorderedCompletedLocalTasksCollection = new List<InternalLocalQueueTask>();
                GlobalQueue = new Queue<InternalGlobalQueueTask>();
                LocalQueue = new Queue<InternalLocalQueueTask>();
                IsLocalQueueEmpty = true;
                ResponseContainer = new ResponseContainer(success: true);
            }

            public void LockGlobalQueue()
            {
                Monitor.Enter(globalQueueSynchronization);
            }

            public void UnlockGlobalQueue()
            {
                Monitor.Exit(globalQueueSynchronization);
            }

            public void LockLocalQueue()
            {
                Monitor.Enter(localQueueSynchronization);
            }

            public void UnlockLocalQueue()
            {
                Monitor.Exit(localQueueSynchronization);
            }

            public void LockFlushBuffer()
            {
                Monitor.Enter(flushBufferSynchronization);
            }

            public void UnlockFlushBuffer()
            {
                Monitor.Pulse(flushBufferSynchronization);
                Monitor.Exit(flushBufferSynchronization);
            }

            public void WaitFlushBufferWhile(Func<bool> func)
            {
                Monitor.Enter(flushBufferSynchronization);

                while(func())
                {
                    Monitor.Wait(flushBufferSynchronization);
                }

                Monitor.Exit(flushBufferSynchronization);
            }
        }
    }
}
