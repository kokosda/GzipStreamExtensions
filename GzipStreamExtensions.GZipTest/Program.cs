namespace GzipStreamExtensions.GZipTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var test = new[]
            {
                "decompress",
                "resources/workingset01.mp4.gz",
                "resources/workingset01.mp4",
            };

            Bootstrapper.Start(test);
        }
    }
}
