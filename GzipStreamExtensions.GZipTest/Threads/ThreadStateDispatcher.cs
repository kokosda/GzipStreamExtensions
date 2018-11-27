using System;
using System.Threading;

namespace GzipStreamExtensions.GZipTest.Threads
{
    internal sealed class ThreadStateDispatcher : IThreadStateDispatcher
    {
        public ThreadStateDispatcherEnqueueResult<T> EnqueueTask<T>(ThreadTask<T> threadTask)
        {
            if (threadTask == null)
                throw new ArgumentNullException(nameof(threadTask));

            var threadsCount = GetAvailableThreadsCount(threadTask.DesiredThreadsCount);
            var threadStates = new ThreadState<T>[Environment.ProcessorCount];

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

            foreach(var threadState in threadStates)
            {
                threadState.Start();
            }
        }

        public int GetAvailableThreadsCount(int? desiredThreadsCount = null)
        {
            var result = desiredThreadsCount ?? 0;

            if (result <= 0 || result > Environment.ProcessorCount)
                result = Environment.ProcessorCount;

            return result;
        }

        public void WaitTaskCompleted()
        {
            Thread.CurrentThread.Join();
        }
    }
}
