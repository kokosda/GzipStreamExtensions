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

        private Queue<InternalTask> ToInternalQueue(long[] seekPoints)
        {
            var result = new Queue<InternalTask>();

            for (int i = 0; i < seekPoints.Length; i++)
            {
                var internalTask = new InternalTask
                {
                    SeekPoint = seekPoints[i]
                };

                result.Enqueue(internalTask);
            }

            return result;
        }

        private ThreadTask<State> GetThreadTask(Queue<InternalTask> queue, FileTaskDescriptor fileTaskDescriptor)
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

                    InternalTask localQueueTask = null;

                    if (!WorkWithGlobalQueue(state, out localQueueTask))
                        return;

                    if (localQueueTask == null)
                    {
                        if (!WorkWithLocalQueue(state, out localQueueTask))
                            continue;
                    }

                    var fileOperationStrategy = state.FileTaskDescriptor.FileOperationStrategy;
                    var processedBytes = fileOperationStrategy.Read(localQueueTask.ReadBuffer, localQueueTask.ReadBufferOffset, localQueueTask.ReadBufferSize);

                    if (!processedBytes.Any())
                        continue;

                    byte[] flushBuffer;
                    int flushBytesCount;

                    WorkWithFlushBuffer(state, localQueueTask, processedBytes, out flushBuffer, out flushBytesCount);

                    if (flushBuffer != null)
                        fileOperationStrategy.Write(state.TargetFileStream, flushBuffer, bytesCountToWrite: flushBytesCount);
                }
            }
            catch(Exception ex)
            {
                state.IsDone = true;
                state.ResponseContainer.AddErrorMessage($"Error while performing background work. Message: {ex.Message}{Environment.NewLine}{ex.StackTrace}.");
            }
        }

        private bool WorkWithGlobalQueue(State state, out InternalTask localQueueTask)
        {
            var result = true;
            localQueueTask = null;

            if (!state.IsLocalQueueEmpty)
                return result;

            state.LockGlobalQueue();

            if (!state.IsLocalQueueEmpty || state.IsDone)
            {
                state.UnlockGlobalQueue();
                return result;
            }

            var globalQueueTask = state.GlobalQueue.Count > 0 ? state.GlobalQueue.Dequeue() : null;

            if (globalQueueTask == null)
            {
                state.IsDone = true;
                state.UnlockGlobalQueue();
                return false;
            }

            state.SourceFileStream.Seek(globalQueueTask.SeekPoint, SeekOrigin.Begin);
            state.ReadBuffer = new byte[defaultReadBufferSize];
            state.ReadBufferSize = state.SourceFileStream.Read(state.ReadBuffer, 0, defaultReadBufferSize);
            state.LocalQueue = GetLocalQueue(state.ReadBuffer, state.ReadBufferSize);
            localQueueTask = state.LocalQueue.Dequeue();
            localQueueTask.ReadBuffer = state.ReadBuffer;
            state.IsLocalQueueEmpty = !state.LocalQueue.Any();

            log.LogInfo($"Working with range of bytes {globalQueueTask.SeekPoint} - {globalQueueTask.SeekPoint + state.ReadBufferSize}.");

            state.UnlockGlobalQueue();

            return result;
        }

        private bool WorkWithLocalQueue(State state, out InternalTask localQueueTask)
        {
            var result = true;
            localQueueTask = null;

            if (state.IsLocalQueueEmpty)
                return false; ;

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

        private void WorkWithFlushBuffer(State state, InternalTask localQueueTask, byte[] processedBytes, out byte[] flushBuffer, out int flushBytesCount)
        {
            flushBuffer = null;
            flushBytesCount = 0;

            state.LockFlushBuffer();

            var canBufferize = (state.FlushBufferSize - state.TotalBytesCountWroteToFlushBuffer) >= processedBytes.Length;

            if (canBufferize)
            {
                state.UnorderedFlushBufferBytesCollection.Add(localQueueTask.ReadBufferOffset, processedBytes);
                state.TotalBytesCountWroteToFlushBuffer += processedBytes.Length;
            }
            else
            {
                flushBuffer = new byte[state.TotalBytesCountWroteToFlushBuffer];
                var localOffset = 0;

                foreach (var pair in state.UnorderedFlushBufferBytesCollection.OrderBy(x => x.Key))
                {
                    Array.Copy(pair.Value, 0, flushBuffer, localOffset, pair.Value.Length);
                    localOffset += pair.Value.Length;
                }

                flushBytesCount = state.TotalBytesCountWroteToFlushBuffer;
                state.UnorderedFlushBufferBytesCollection.Clear();
                state.TotalBytesCountWroteToFlushBuffer = 0;
            }

            state.UnlockFlushBuffer();
        }

        private Queue<InternalTask> GetLocalQueue(byte[] buffer, int bufferSize)
        {
            var result = new Queue<InternalTask>();
            var threadsCount = threadStateDispatcher.GetAvailableThreadsCount();
            var localBufferSize = (int)Math.Ceiling((double)defaultLocalReadBufferSize / threadsCount);
            var tasksCount = (int)Math.Ceiling((double)bufferSize / localBufferSize);
            var offset = 0;

            for (int i = 0; i < tasksCount; i++)
            {
                var tempBufferSize = localBufferSize;
                var tempPosition = offset + localBufferSize;

                if (tempPosition > bufferSize)
                    tempBufferSize = bufferSize - offset;

                var task = new InternalTask
                {
                    ReadBufferOffset = offset,
                    ReadBufferSize = tempBufferSize
                };
                result.Enqueue(task);

                offset += tempBufferSize;
            }
            return result;
        }

        private class InternalTask
        {
            public InternalTask()
            {
                ReadBuffer = new byte[0];
            }

            public long SeekPoint { get; set; }
            public int ReadBufferOffset { get; set; }
            public int ReadBufferSize { get; set; }
            public byte[] ReadBuffer { get; set; }
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

            public Queue<InternalTask> GlobalQueue { get; set; }
            public Queue<InternalTask> LocalQueue { get; set; }
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
            public int TotalBytesCountWroteToReadBuffer { get; set; }
            public int FlushBufferSize { get; set; }
            public int TotalBytesCountWroteToFlushBuffer { get; set; }
            public Dictionary<int, byte[]> UnorderedFlushBufferBytesCollection { get; set; }
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
                UnorderedFlushBufferBytesCollection = new Dictionary<int, byte[]>();
                GlobalQueue = new Queue<InternalTask>();
                LocalQueue = new Queue<InternalTask>();
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
