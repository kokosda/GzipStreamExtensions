using GzipStreamExtensions.GZipTest.Facilities;
using GzipStreamExtensions.GZipTest.Services.Abstract;
using GzipStreamExtensions.GZipTest.Threads;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileOperationsManager : IFileOperationsManager
    {
        private readonly IThreadStateDispatcher threadStateDispatcher;
        private readonly int defaultReadBufferSize = 1 * 1024 * 1024;
        private readonly int defaultFlushBufferSize = 16 * 1024 * 1024;

        public FileOperationsManager(IThreadStateDispatcher threadStateDispatcher)
        {
            this.threadStateDispatcher = threadStateDispatcher;
        }

        public ResponseContainer RunByFileTaskDescriptor(FileTaskDescriptor fileTaskDescriptor)
        {
            var result = new ResponseContainer(success: true);

            try
            {
                var seekPoints = GetSeekPoints(fileTaskDescriptor.FileLength);
                var queue = ToInternalQueue(seekPoints);
                var threadTask = GetThreadTask(queue, fileTaskDescriptor);
                var enqueueResult = threadStateDispatcher.EnqueueTask(threadTask);

                threadStateDispatcher.StartTask(enqueueResult);
                threadStateDispatcher.WaitTaskCompleted();

                result.Join(threadTask.State.ResponseContainer);
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
                    Queue = queue,
                    FileTaskDescriptor = fileTaskDescriptor,
                    FlushBufferSize = defaultFlushBufferSize
                }
            };
            return result;
        }

        private void Work(State state)
        {
            var queue = state.Queue;            
            var fileTaskDescriptor = state.FileTaskDescriptor;

            try
            {
                using (var sourceFileStream = new FileStream(fileTaskDescriptor.SourceFilePath, FileMode.Open, FileAccess.Read))
                using (var targetFileStream = new FileStream(fileTaskDescriptor.TargetFilePath, FileMode.Append, FileAccess.Write))
                {
                    while (true)
                    {
                        state.LockQueue();

                        if (!state.ResponseContainer.Success)
                        {
                            state.UnlockQueue();
                            return;
                        }

                        var internalTask = queue.Count > 0 ? queue.Dequeue() : null;
                        state.UnlockQueue();

                        if (internalTask == null)
                            return;

                        var fileOperationStrategy = fileTaskDescriptor.FileOperationStrategy;
                        var bytesRead = fileOperationStrategy.Read(sourceFileStream, internalTask.SeekPoint, defaultReadBufferSize);
                        byte[] flushBuffer = null;
                        int flushBytesCount = 0;

                        state.LockFlushBuffer();

                        var canBufferize = (state.FlushBufferSize - state.TotalBytesCountWroteToFlushBuffer) >= bytesRead.Length;

                        if (canBufferize)
                        {
                            state.UnorderedBytesCollection.Add(internalTask.SeekPoint, bytesRead);
                            state.TotalBytesCountWroteToFlushBuffer += bytesRead.Length;
                        }
                        else
                        {
                            flushBuffer = new byte[state.TotalBytesCountWroteToFlushBuffer];
                            var localOffset = 0;

                            foreach (var pair in state.UnorderedBytesCollection.OrderBy(x => x.Key))
                            {
                                Array.Copy(pair.Value, 0, flushBuffer, localOffset, pair.Value.Length);
                                localOffset += pair.Value.Length;
                            }

                            flushBytesCount = state.TotalBytesCountWroteToFlushBuffer;
                            state.UnorderedBytesCollection.Clear();
                            state.TotalBytesCountWroteToFlushBuffer = 0;
                        }

                        state.UnlockFlushBuffer();

                        if (flushBuffer != null)
                            fileOperationStrategy.Write(targetFileStream, flushBuffer, bytesCountToWrite: flushBytesCount);
                    }
                }
            }
            catch(Exception ex)
            {
                state.LockQueue();
                state.ResponseContainer.AddErrorMessage($"Error while performing background work. Message: {ex.Message}{Environment.NewLine}{ex.StackTrace}.");
                state.UnlockQueue();
            }
        }

        private class InternalTask
        {
            public long SeekPoint { get; set; }
        }

        private class State
        {
            private readonly object queueSynchronization = new object();
            private readonly object flushBufferSynchronization = new object();

            public Queue<InternalTask> Queue { get; set; }
            public FileTaskDescriptor FileTaskDescriptor { get; set; }
            public int FlushBufferSize { get; set; }
            public int TotalBytesCountWroteToFlushBuffer { get; set; }
            public Dictionary<long, byte[]> UnorderedBytesCollection { get; set; }
            public ResponseContainer ResponseContainer { get; set; }

            public State()
            {
                UnorderedBytesCollection = new Dictionary<long, byte[]>();
                Queue = new Queue<InternalTask>();
                ResponseContainer = new ResponseContainer(success: true);
            }

            public void LockQueue()
            {
                Monitor.Enter(queueSynchronization);
            }

            public void UnlockQueue()
            {
                Monitor.Exit(queueSynchronization);
            }

            public void LockFlushBuffer()
            {
                Monitor.Enter(flushBufferSynchronization);
            }

            public void UnlockFlushBuffer()
            {
                Monitor.Exit(flushBufferSynchronization);
            }
        }
    }
}
