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
                    SeekPoint = readBufferSize * i
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
                    if (!WorkWithGlobalQueue(state))
                        return;

                    if (!WorkWithReadLocalQueue(state))
                        continue;
                    
                    WorkWithWriteLocalQueue(state);
                }
            }
            catch(Exception ex)
            {
                state.IsWorkDone = true;
                state.ResponseContainer.AddErrorMessage($"Error while performing background work. Message: {ex.Message}{Environment.NewLine}{ex.StackTrace}.");
            }
        }

        private bool WorkWithGlobalQueue(State state)
        {
            var result = true;

            if (state.IsWorkDone)
                return false;

            if (state.IsReadyToWrite || !state.IsReadLocalQueueEmpty || state.IsGlobalTaskDoing)
                return result;

            state.LockGlobalQueue();

            if (state.IsWorkDone)
            {
                state.UnlockGlobalQueue();
                return false;
            }

            if (state.IsReadyToWrite || !state.IsReadLocalQueueEmpty || state.IsGlobalTaskDoing)
            {
                state.UnlockGlobalQueue();
                return result;
            }

            var globalQueueTask = state.GlobalQueue.Any() ? state.GlobalQueue.Dequeue() : null;

            if (globalQueueTask == null)
            {
                log.LogInfo($"Marking done. IsReadyToWrite={state.IsReadyToWrite}, IsReadLocalQueueEmpty={state.IsReadLocalQueueEmpty}, write queue count {state.WriteLocalQueue.Count}");
                state.IsWorkDone = true;
                state.UnlockGlobalQueue();
                return false;
            }

            state.IsGlobalTaskDoing = true;
            var strategyParameters = state.FileTaskDescriptor.FileOperationStrategyParameters;
            state.ReadBufferSize = strategyParameters.ReadBufferSize;
            //state.ReadBufferSize = globalQueueTask.SeekPoint + strategyParameters.ReadBufferSize < state.FileTaskDescriptor.FileLength
            //    ? strategyParameters.ReadBufferSize
            //    : checked((int)(state.FileTaskDescriptor.FileLength - globalQueueTask.SeekPoint));
            state.ReadBuffer = new byte[state.ReadBufferSize];
            state.ReadLocalQueue = GetReadLocalQueue(
                buffer: state.ReadBuffer,
                bufferSize: state.ReadBufferSize,
                tasksCount: strategyParameters.ReadBufferChunks);
            state.IsReadLocalQueueEmpty = !state.ReadLocalQueue.Any();

            log.LogInfo($"Working with range of bytes {globalQueueTask.SeekPoint} - {globalQueueTask.SeekPoint + state.ReadBufferSize}.");

            state.UnlockGlobalQueue();

            return result;
        }

        private Queue<InternalLocalQueueTask> GetReadLocalQueue(byte[] buffer, int bufferSize, int tasksCount)
        {
            var result = new Queue<InternalLocalQueueTask>();
            var localBufferSize = (int)Math.Ceiling((double)bufferSize / tasksCount);
            var localOffset = 0;

            for (int i = 0; i < tasksCount; i++)
            {
                var tempBufferSize = localBufferSize;
                var tempPosition = localOffset + localBufferSize;

                if (tempPosition > bufferSize)
                    tempBufferSize = bufferSize - localOffset;

                var task = new InternalLocalQueueTask
                {
                    Buffer = buffer,
                    BufferOffset = localOffset,
                    BufferSize = tempBufferSize
                };
                result.Enqueue(task);

                localOffset += tempBufferSize;
            }
            return result;
        }

        private bool WorkWithReadLocalQueue(State state)
        {
            var result = true;

            if (state.IsReadyToWrite)
                return true;

            if (state.IsReadLocalQueueEmpty)
                return false;

            state.LockReadLocalQueue();

            if (state.IsReadyToWrite)
            {
                state.UnlockReadLocalQueue();
                return true;
            }

            if (state.IsReadLocalQueueEmpty)
            {
                state.UnlockReadLocalQueue();
                return false;
            }

            var readLocalQueueTask = state.ReadLocalQueue.Dequeue();

            if (!state.ReadLocalQueue.Any())
                state.IsReadLocalQueueEmpty = true;

            if (state.IsReadyToWrite)
                state.WaitWriteLocalQueueWhile(() => state.IsReadyToWrite);

            var strategy = state.FileTaskDescriptor.FileOperationStrategy;
            var strategyParameters = state.FileTaskDescriptor.FileOperationStrategyParameters;
            var mutableStrategyParameters = new FileOperationStrategyMutableParameters
            {
                Buffer = readLocalQueueTask.Buffer,
                Offset = readLocalQueueTask.BufferOffset,
                BufferSize = readLocalQueueTask.BufferSize
            };
            strategy.Read(strategyParameters, mutableStrategyParameters);

            if (mutableStrategyParameters.IsCompleted)
            {
                state.ReadLocalQueue.Clear();
                state.IsReadLocalQueueEmpty = true;
            }

            EnqueueTasksToWriteLocalQueue(
                state: state,
                mutableStrategyParameters: mutableStrategyParameters,
                buffer: readLocalQueueTask.Buffer,
                offset: readLocalQueueTask.BufferOffset,
                bufferSize: mutableStrategyParameters.BytesRead, 
                tasksCount: strategyParameters.WriteBufferChunks);

            state.TotalBytesCountInWriteLocalQueue += mutableStrategyParameters.BytesRead;
            state.IsReadyToWrite = state.TotalBytesCountInWriteLocalQueue >= strategyParameters.WriteBufferSize ||
                state.WriteLocalQueue.Count >= strategyParameters.WriteBufferChunks || 
                mutableStrategyParameters.IsCompleted;
            result = state.IsReadyToWrite;

            state.UnlockReadLocalQueue();

            return result;
        }

        private void EnqueueTasksToWriteLocalQueue(State state, 
            FileOperationStrategyMutableParameters mutableStrategyParameters, 
            byte[] buffer, int offset, int bufferSize, int tasksCount)
        {
            var localBufferSize = (int)Math.Ceiling((double)bufferSize / tasksCount);
            var localOffset = 0;

            log.LogInfo($"Tasks count {tasksCount}");

            for (int i = 0; i < tasksCount; i++)
            {
                var task = new InternalLocalQueueTask
                {
                    Buffer = buffer,
                    BufferOffset = offset + localOffset,
                    BufferSize = (localOffset + localBufferSize) < bufferSize ? localBufferSize : bufferSize - localOffset
                };

                state.WriteLocalQueue.Enqueue(task);

                task.IsTerminal = mutableStrategyParameters.IsCompleted && (i + 1) == tasksCount;
                localOffset += localBufferSize;

                log.LogInfo($"Enqueue Write task {task.BufferOffset} {task.BufferSize} {task.IsTerminal}. Tasks count {state.WriteLocalQueue.Count}");
            }
        }

        private void WorkWithWriteLocalQueue(State state)
        {
            if (!state.IsReadyToWrite)
                return;

            state.LockWriteLocalQueue();

            if (!state.IsReadyToWrite)
            {
                state.UnlockWriteLocalQueue(notifyWaitingTasks: true);
                return;
            }

            var strategy = state.FileTaskDescriptor.FileOperationStrategy;
            var strategyParameters = state.FileTaskDescriptor.FileOperationStrategyParameters;
            var writeLocalQueueTask = state.WriteLocalQueue.Dequeue();

            log.LogInfo($"Dequeued Write task {writeLocalQueueTask.BufferOffset} {writeLocalQueueTask.BufferSize} {writeLocalQueueTask.IsTerminal}. Tasks count {state.WriteLocalQueue.Count}");

            var mutableStrategyParameters = new FileOperationStrategyMutableParameters
            {
                Buffer = writeLocalQueueTask.Buffer,
                Offset = writeLocalQueueTask.BufferOffset,
                BufferSize = writeLocalQueueTask.BufferSize,
                IsCompleted = writeLocalQueueTask.IsTerminal
            };

            strategy.Write(strategyParameters, mutableStrategyParameters);

            state.IsReadyToWrite = state.WriteLocalQueue.Any();

            var notifyWaitingTasks = false;

            if (!state.IsReadyToWrite)
            {
                state.TotalBytesCountInWriteLocalQueue = 0;
                notifyWaitingTasks = true;
                state.IsGlobalTaskDoing = false;
            }

            state.UnlockWriteLocalQueue(notifyWaitingTasks);
        }

        private class InternalGlobalQueueTask
        {
            public long SeekPoint { get; set; }
        }

        private class InternalLocalQueueTask
        {
            private volatile bool isTerminal;

            public int BufferOffset { get; set; }
            public int BufferSize { get; set; }
            public byte[] Buffer { get; set; }
            public bool IsTerminal
            {
                get { return isTerminal; }
                set { isTerminal = value; }
            }

            public InternalLocalQueueTask()
            {
                Buffer = new byte[0];
            }
        }

        private class State
        {
            private readonly object globalQueueSynchronization = new object();
            private readonly object readLocalQueueSynchronization = new object();
            private readonly object writeLocalQueueSynchronization = new object();
            private volatile bool isReadLocalQueueEmpty;
            private volatile bool isReadyToWrite;
            private volatile bool isDone;
            private volatile int totalBytesCountInWriteLocalQueue;
            private InternalGlobalQueueTask globalQueueTask;
            private bool isGlobalTaskDoing;

            public ThreadStateDispatcherEnqueueResult<State> EnqueueResult { get; set; }
            public Queue<InternalGlobalQueueTask> GlobalQueue { get; set; }
            public Queue<InternalLocalQueueTask> ReadLocalQueue { get; set; }
            public Queue<InternalLocalQueueTask> WriteLocalQueue { get; set; }
            public FileTaskDescriptor FileTaskDescriptor { get; set; }
            public byte[] ReadBuffer { get; set; }
            public int ReadBufferSize { get; set; }
            public int TotalBytesCountInWriteLocalQueue
            {
                get { return totalBytesCountInWriteLocalQueue; }
                set { totalBytesCountInWriteLocalQueue = value; }
            }
            public ResponseContainer ResponseContainer { get; set; }
            public bool IsWorkDone
            {
                get { return isDone; }
                set { isDone = value; }
            }
            public bool IsGlobalTaskDoing
            {
                get => isGlobalTaskDoing;
                set => isGlobalTaskDoing = value;
            }
            public bool IsReadLocalQueueEmpty
            {
                get { return isReadLocalQueueEmpty; }
                set { isReadLocalQueueEmpty = value; }
            }
            public bool IsReadyToWrite
            {
                get { return isReadyToWrite; }
                set { isReadyToWrite = value; }
            }
            public bool IsAwaitable { get; set; }

            public State()
            {
                GlobalQueue = new Queue<InternalGlobalQueueTask>();
                ReadLocalQueue = new Queue<InternalLocalQueueTask>();
                WriteLocalQueue = new Queue<InternalLocalQueueTask>();
                IsReadLocalQueueEmpty = true;
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

            public void LockReadLocalQueue()
            {
                Monitor.Enter(readLocalQueueSynchronization);
            }

            public void UnlockReadLocalQueue()
            {
                Monitor.Exit(readLocalQueueSynchronization);
            }

            public void LockWriteLocalQueue()
            {
                Monitor.Enter(writeLocalQueueSynchronization);
            }

            public void UnlockWriteLocalQueue(bool notifyWaitingTasks)
            {
                //if (notifyWaitingTasks)
                //    Monitor.Pulse(writeLocalQueueSynchronization);

                Monitor.Exit(writeLocalQueueSynchronization);
            }

            public void WaitWriteLocalQueueWhile(Func<bool> func)
            {
                if (!IsAwaitable)
                    return;

                Monitor.Enter(writeLocalQueueSynchronization);

                while (func())
                {
                    Monitor.Wait(writeLocalQueueSynchronization);
                }

                Monitor.Exit(writeLocalQueueSynchronization);
            }
        }
    }
}
