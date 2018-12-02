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
        private readonly int defaultReadBufferSize = 16 * 1024 * 1024;
        private readonly int defaultLocalReadBufferSize = 1 * 1024 * 1024;
        private readonly int defaultFlushBufferSize = 16 * 1024 * 1024;

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
                var seekPoints = GetSeekPoints(fileTaskDescriptor.FileLength);
                var queue = ToInternalQueue(seekPoints);
                var threadTask = GetThreadTask(queue, fileTaskDescriptor);
                var enqueueResult = threadStateDispatcher.EnqueueTask(threadTask);
                threadTask.State.EnqueueResult = enqueueResult;

                threadStateDispatcher.StartTask(enqueueResult);

                log.LogInfo("Please wait...");

                threadStateDispatcher.WaitTaskCompleted();
                threadTask.State.Dispose();
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

        private long[] GetSeekPoints(long fileLength)
        {
            if (fileLength == 0)
                return new long[1] { 0 };

            var count = (long)Math.Ceiling(fileLength / (double)defaultReadBufferSize);
            var result = new long[count];

            for (long i = 0; i < count; i++)
            {
                result[i] = defaultReadBufferSize * i;
            }

            return result;
        }

        private Queue<InternalGlobalQueueTask> ToInternalQueue(long[] seekPoints)
        {
            var result = new Queue<InternalGlobalQueueTask>();

            for (int i = 0; i < seekPoints.Length; i++)
            {
                var internalTask = new InternalGlobalQueueTask
                {
                    SeekPoint = seekPoints[i],
                    Order = i
                };

                result.Enqueue(internalTask);
            }

            return result;
        }

        private ThreadTask<State> GetThreadTask(Queue<InternalGlobalQueueTask> queue, FileTaskDescriptor fileTaskDescriptor)
        {
            var result = new ThreadTask<State>
            {
                Action = Work,
                DesiredThreadsCount = queue.Count,
                State = new State
                {
                    GlobalQueue = queue,
                    FileTaskDescriptor = fileTaskDescriptor,
                    FlushBufferSize = defaultFlushBufferSize,
                    SourceFileStream = new FileStream(fileTaskDescriptor.SourceFilePath, FileMode.Open, FileAccess.Read),
                    TargetFileStream = new FileStream(fileTaskDescriptor.TargetFilePath, FileMode.Append, FileAccess.Write)
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

                    var fileOperationStrategy = state.FileTaskDescriptor.FileOperationStrategy;
                    var processedBytes = fileOperationStrategy.Read(localQueueTask.ReadBuffer, localQueueTask.ReadBufferOffset, localQueueTask.ReadBufferSize);

                    
                    WorkWithFlushBuffer(state, localQueueTask, processedBytes);
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

            state.SourceFileStream.Seek(globalQueueTask.SeekPoint, SeekOrigin.Begin);
            state.ReadBuffer = new byte[defaultReadBufferSize];
            state.ReadBufferSize = state.SourceFileStream.Read(state.ReadBuffer, 0, defaultReadBufferSize);
            state.LocalQueue = GetLocalQueue(state, state.CurrentGlobalQueueTask, state.ReadBuffer, state.ReadBufferSize);
            state.CurrentGlobalQueueTask.TasksCount = state.LocalQueue.Count;
            state.IsLocalQueueEmpty = !state.LocalQueue.Any();

            log.LogInfo($"Working with range of bytes {state.CurrentGlobalQueueTask.SeekPoint} - {state.CurrentGlobalQueueTask.SeekPoint + state.ReadBufferSize}.");

            state.UnlockGlobalQueue();

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
            localQueueTask.ReadBuffer = state.ReadBuffer;

            if (!state.LocalQueue.Any())
                state.IsLocalQueueEmpty = true;

            state.UnlockLocalQueue();

            return result;
        }

        private void WorkWithFlushBuffer(State state, InternalLocalQueueTask localQueueTask, byte[] processedBytes)
        {
            if (localQueueTask.GlobalQueueTask.Order > state.FlushGlobalQueueTask.Order)
            {
                state.WaitFlushBufferWhile(() => state.CurrentGlobalQueueTask.Order > state.FlushGlobalQueueTask.Order);
            }

            state.LockFlushBuffer();

            localQueueTask.WriteBuffer = processedBytes;
            state.UnorderedCompletedLocalTasksCollection.Add(localQueueTask);
            state.TotalBytesCountWroteToFlushBuffer = checked(state.TotalBytesCountWroteToFlushBuffer + processedBytes.Length);

            var isReadyToFlush = state.UnorderedCompletedLocalTasksCollection.Count == state.FlushGlobalQueueTask.TasksCount;

            if (!isReadyToFlush)
            {
                state.UnlockFlushBuffer();
                return;
            }

            var flushBuffer = new byte[state.TotalBytesCountWroteToFlushBuffer];
            var localOffset = 0;

            foreach (var task in state.UnorderedCompletedLocalTasksCollection.OrderBy(x => x.Order))
            {
                Array.Copy(task.WriteBuffer, 0, flushBuffer, localOffset, task.WriteBuffer.Length);
                localOffset += task.WriteBuffer.Length;
            }

            var flushBytesCount = state.TotalBytesCountWroteToFlushBuffer;
            var fileOperationStrategy = state.FileTaskDescriptor.FileOperationStrategy;
            fileOperationStrategy.Write(state.TargetFileStream, flushBuffer, bytesCountToWrite: flushBytesCount);

            state.UnorderedCompletedLocalTasksCollection.Clear();
            state.TotalBytesCountWroteToFlushBuffer = 0;
            state.FlushGlobalQueueTask = state.CurrentGlobalQueueTask;

            state.UnlockFlushBuffer();
        }

        private Queue<InternalLocalQueueTask> GetLocalQueue(State state, InternalGlobalQueueTask globalQueueTask, byte[] buffer, int bufferSize)
        {
            var result = new Queue<InternalLocalQueueTask>();
            var threadsCount = state.EnqueueResult.ThreadStates.Count();
            var localBufferSize = (int)Math.Ceiling((double)defaultLocalReadBufferSize / threadsCount);
            var tasksCount = (int)Math.Ceiling((double)bufferSize / localBufferSize);
            var offset = 0;

            for (int i = 0; i < tasksCount; i++)
            {
                var tempBufferSize = localBufferSize;
                var tempPosition = offset + localBufferSize;

                if (tempPosition > bufferSize)
                    tempBufferSize = bufferSize - offset;

                var task = new InternalLocalQueueTask(globalQueueTask)
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

        private class InternalGlobalQueueTask
        {
            private volatile int tasksCount;
            private volatile int order;

            public long SeekPoint { get; set; }
            public int TasksCount
            {
                get { return tasksCount; }
                set { tasksCount = value; }
            }
            public int Order
            {
                get { return order; }
                set { order = value; }
            }
        }

        private class InternalLocalQueueTask
        {
            public InternalLocalQueueTask(InternalGlobalQueueTask globalQueueTask)
            {
                ReadBuffer = new byte[0];
                WriteBuffer = new byte[0];
                GlobalQueueTask = globalQueueTask;
            }

            public int ReadBufferOffset { get; set; }
            public int ReadBufferSize { get; set; }
            public byte[] ReadBuffer { get; set; }
            public byte[] WriteBuffer { get; set; }
            public int Order { get; set; }
            public InternalGlobalQueueTask GlobalQueueTask { get; set; }
        }

        private class State : IDisposable
        {
            private readonly object globalQueueSynchronization = new object();
            private readonly object localQueueSynchronization = new object();
            private readonly object flushBufferSynchronization = new object();
            private volatile bool isLocalQueueEmpty;
            private volatile bool isDone;
            private volatile byte[] readBuffer;
            private volatile int readBufferSize;
            private volatile InternalGlobalQueueTask globalQueueTask;
            private volatile InternalGlobalQueueTask flushGlobalQueueTask;

            public ThreadStateDispatcherEnqueueResult<State> EnqueueResult { get; set; }
            public Queue<InternalGlobalQueueTask> GlobalQueue { get; set; }
            public Queue<InternalLocalQueueTask> LocalQueue { get; set; }
            public FileTaskDescriptor FileTaskDescriptor { get; set; }
            public FileStream SourceFileStream { get; set; }
            public FileStream TargetFileStream { get; set; }
            public byte[] ReadBuffer
            {
                get { return readBuffer; }
                set { readBuffer = value; }
            }
            public int ReadBufferSize
            {
                get { return readBufferSize; }
                set { readBufferSize = value; }
            }
            public InternalGlobalQueueTask CurrentGlobalQueueTask
            {
                get { return globalQueueTask; }
                set { globalQueueTask = value; }
            }
            public InternalGlobalQueueTask FlushGlobalQueueTask
            {
                get { return flushGlobalQueueTask;  }
                set { flushGlobalQueueTask = value; }
            }
            public int TotalBytesCountWroteToReadBuffer { get; set; }
            public int FlushBufferSize { get; set; }
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

            public void Dispose()
            {
                SourceFileStream.Dispose();
                TargetFileStream.Dispose();
            }
        }
    }
}
