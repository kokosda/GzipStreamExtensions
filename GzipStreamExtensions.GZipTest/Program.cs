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
                "compress",
                "resources/workingset01.mp4",
                "resources/workingset01.mp4.gz",

            };

            Bootstrapper.Start(test);
        }
    }
}
