using System;

namespace GzipStreamExtensions.GZipTest.Threads
{
    public sealed class ThreadTask<T>
    {
        public int DesiredThreadsCount { get; set; }
        public Action<T> Action { get; set; }
        public T State { get; set; }
    }
}
