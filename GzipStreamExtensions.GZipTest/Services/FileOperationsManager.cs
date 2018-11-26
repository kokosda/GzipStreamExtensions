using GzipStreamExtensions.GZipTest.Threads;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileOperationsManager
    {
        private readonly ThreadStateDispatcher threadStateDispatcher;
        private readonly int defaultFlushBufferSize = 16 * 1024 * 1024;

        public FileOperationsManager(ThreadStateDispatcher threadStateDispatcher)
        {
            this.threadStateDispatcher = threadStateDispatcher;
        }

        public void RunByFileTaskDescriptor(FileTaskDescriptor fileTaskDescriptor)
        {
            ValidateFileTaskDescriptor(fileTaskDescriptor);

            var seekPoints = GetSeekPoints(fileTaskDescriptor.FileLength, fileTaskDescriptor.ReadBufferSize);
            var queue = ToInternalQueue(seekPoints);
            var threadTask = GetThreadTask(queue, fileTaskDescriptor);
            var enqueueResult = threadStateDispatcher.EnqueueTask(threadTask);

            threadStateDispatcher.StartTask(enqueueResult);
        }

        private long[] GetSeekPoints(long fileLength, int readBufferSize)
        {
            if (fileLength == 0)
                return new long[1] { 0 };

            var count = (long)Math.Ceiling(fileLength / (double)readBufferSize);
            var result = new long[count];

            for (long i = 0; i < count; i++)
            {
                result[i] = readBufferSize * i;
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
            }

            return result;
        }

        private ThreadTask<State> GetThreadTask(Queue<InternalTask> queue, FileTaskDescriptor fileTaskDescriptor)
        {
            var flushBufferSizeLocal = GetFlushBufferSize(fileTaskDescriptor);
            var result = new ThreadTask<State>
            {
                Action = Work,
                DesiredThreadsCount = queue.Count,
                State = new State
                {
                    Queue = queue,
                    FileTaskDescriptor = fileTaskDescriptor,
                    FlushBuffer = new byte[flushBufferSizeLocal]
                }
            };
            return result;
        }

        private int GetFlushBufferSize(FileTaskDescriptor fileTaskDescriptor)
        {
            var result = defaultFlushBufferSize;
            
            if (result < fileTaskDescriptor.ReadBufferSize)
                result = checked(fileTaskDescriptor.ReadBufferSize * threadStateDispatcher.GetAvailableThreadsCount(desiredThreadsCount: 0) * 2);

            return result;
        }

        private void Work(State state)
        {
            var queue = state.Queue;            
            var fileTaskDescriptor = state.FileTaskDescriptor;

            using (var sourceFileStream = new FileStream(fileTaskDescriptor.SourceFilePath, FileMode.Open, FileAccess.Read))
            using (var targetFileStream = new FileStream(fileTaskDescriptor.TargetFilePath, FileMode.Append, FileAccess.Write))
            {
                while (true)
                {
                    state.LockQueue();
                    var internalTask = queue.Count > 0 ? queue.Dequeue() : null;
                    state.UnlockQueue();

                    if (internalTask == null)
                        return;

                    var fileOperationStrategy = fileTaskDescriptor.FileOperationStrategy;
                    var bytesRead = fileOperationStrategy.Read(sourceFileStream, internalTask.SeekPoint, fileTaskDescriptor.ReadBufferSize);
                    byte[] flushBuffer = null;
                    int flushBytesCount = 0;

                    state.LockFlushBuffer();

                    var canBufferize = (state.FlushBuffer.Length - state.TotalBytesCountWroteToFlushBuffer) >= bytesRead.Length;

                    if (canBufferize)
                    {
                        Array.Copy(sourceArray: bytesRead,
                                    sourceIndex: 0,
                                    destinationArray: state.FlushBuffer,
                                    destinationIndex: state.TotalBytesCountWroteToFlushBuffer,
                                    length: bytesRead.Length);
                        state.TotalBytesCountWroteToFlushBuffer += bytesRead.Length;
                    }
                    else
                    {
                        flushBuffer = state.FlushBuffer;
                        flushBytesCount = state.TotalBytesCountWroteToFlushBuffer;
                        state.FlushBuffer = new byte[state.FlushBuffer.Length];
                        state.TotalBytesCountWroteToFlushBuffer = 0;
                    }

                    state.UnlockFlushBuffer();

                    if (flushBuffer != null)
                        fileOperationStrategy.Write(targetFileStream, flushBuffer, bytesCountToWrite: flushBytesCount);
                }
            }
        }

        private void ValidateFileTaskDescriptor(FileTaskDescriptor fileTaskDescriptor)
        {
            if (fileTaskDescriptor == null)
                throw new ArgumentNullException(nameof(fileTaskDescriptor));

            if (string.IsNullOrEmpty(fileTaskDescriptor.SourceFilePath))
                throw new NullReferenceException($"{nameof(fileTaskDescriptor.SourceFilePath)} is not defined.");

            if (string.IsNullOrEmpty(fileTaskDescriptor.TargetFilePath))
                throw new NullReferenceException($"{nameof(fileTaskDescriptor.TargetFilePath)} is not defined.");

            if (fileTaskDescriptor.ReadBufferSize <= 0)
                throw new NotSupportedException($"{nameof(fileTaskDescriptor.ReadBufferSize)} should be positive integer.");
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
            public byte[] FlushBuffer { get; set; }
            public int TotalBytesCountWroteToFlushBuffer { get; set; }

            public State()
            {
                FlushBuffer = new byte[0];
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
