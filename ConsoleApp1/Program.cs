using GzipStreamExtensions.GZipTest;
using System;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = "Resources/WorkingSet01.mp4";
            Operator.CompressAsync(path);
            Console.ReadKey();
        }
    }
}
