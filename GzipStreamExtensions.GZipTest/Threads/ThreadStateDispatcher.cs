using System;

namespace GzipStreamExtensions.GZipTest.Threads
{
    internal sealed class ThreadStateDispatcher
    {
        public ThreadStateDispatcherEnqueueResult<T> EnqueueTask<T>(ThreadTask<T> threadTask)
        {
            if (threadTask == null)
                throw new ArgumentNullException(nameof(threadTask));

            var threadsCount = threadTask.DesiredThreadsCount;

            if (threadsCount <= 0 || threadsCount > Environment.ProcessorCount)
                threadsCount = Environment.ProcessorCount;

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

        public int GetAvailableThreadsCount(int desiredThreadsCount)
        {
            var result = desiredThreadsCount;

            if (result <= 0 || result > Environment.ProcessorCount)
                result = Environment.ProcessorCount;

            return result;
        }
    }
}
