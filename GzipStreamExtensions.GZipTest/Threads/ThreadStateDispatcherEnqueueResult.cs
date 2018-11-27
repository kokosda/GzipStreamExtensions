using System;

namespace GzipStreamExtensions.GZipTest.Threads
{
    public sealed class ThreadStateDispatcherEnqueueResult<T>
    {
        public ThreadStateDispatcherEnqueueResult(ThreadState<T>[] threadStates)
        {
            if (threadStates == null)
                throw new ArgumentNullException(nameof(threadStates));

            ThreadStates = threadStates;
        }

        public ThreadState<T>[] ThreadStates { get; private set; }
    }
}
