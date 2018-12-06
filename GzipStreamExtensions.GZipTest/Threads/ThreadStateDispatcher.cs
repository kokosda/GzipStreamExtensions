using System;
using System.Threading;

namespace GzipStreamExtensions.GZipTest.Threads
{
    internal sealed class ThreadStateDispatcher : IThreadStateDispatcher
    {
        private readonly ManualResetEvent manualResetEvent = new ManualResetEvent(initialState: false);
        private int threadsCount = 0;

        public ThreadStateDispatcherEnqueueResult<T> EnqueueTask<T>(ThreadTask<T> threadTask)
        {
            if (threadTask == null)
                throw new ArgumentNullException(nameof(threadTask));

            var threadsCount = GetAvailableThreadsCount();
            var threadStates = new ThreadState<T>[threadsCount];

            for (int i = 0; i < threadStates.Length; i++)
            {
                threadStates[i] = new ThreadState<T>(threadTask.Action, threadTask.State);
            }

            var result = new ThreadStateDispatcherEnqueueResult<T>(threadStates);
            return result;
        }

        public void StartTask<T>(ThreadStateDispatcherEnqueueResult<T> enqueueResult)
        {
            if (enqueueResult == null)
                throw new ArgumentNullException(nameof(enqueueResult));

            var threadStates = enqueueResult.ThreadStates;
            threadsCount = threadStates.Length;

            foreach(var threadState in threadStates)
            {
                threadState.OnFinished = OnThreadStateFinished;
                threadState.Start();
            }
        }

        public int GetAvailableThreadsCount()
        {
            var result = Environment.ProcessorCount;
            return result;
        }

        public void WaitTaskCompleted()
        {
            manualResetEvent.WaitOne();
        }

        public void Dispose()
        {
            manualResetEvent.Close();
        }

        private void OnThreadStateFinished()
        {
            if (Interlocked.Decrement(ref threadsCount) == 0)
                manualResetEvent.Set();
        }
    }
}
