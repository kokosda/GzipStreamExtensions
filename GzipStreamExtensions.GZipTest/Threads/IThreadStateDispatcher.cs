namespace GzipStreamExtensions.GZipTest.Threads
{
    public interface IThreadStateDispatcher
    {
        ThreadStateDispatcherEnqueueResult<T> EnqueueTask<T>(ThreadTask<T> threadTask);

        void StartTask<T>(ThreadStateDispatcherEnqueueResult<T> enqueueResult);

        int GetAvailableThreadsCount(int? desiredThreadsCount = null);
        void WaitTaskCompleted();
    }
}
