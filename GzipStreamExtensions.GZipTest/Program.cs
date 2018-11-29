using System;

namespace GzipStreamExtensions.GZipTest
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var test = new[]
            {
                "decompress",
                "resources/workingset02.exe.gz",
                "resources/workingset02.exe",

            };

            Bootstrapper.Start(test);
        }
    }
}
