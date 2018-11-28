using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class ConsoleLog : ILog
    {
        public void LogInfo(string message)
        {
            Console.WriteLine(message);
        }
    }
}
