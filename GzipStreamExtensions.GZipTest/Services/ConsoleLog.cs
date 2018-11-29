using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;
using System.Threading;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class ConsoleLog : ILog
    {
        public void LogInfo(string message)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{Thread.CurrentThread.ManagedThreadId}]: {message}");
        }
    }
}
