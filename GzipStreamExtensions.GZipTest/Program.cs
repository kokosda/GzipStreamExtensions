namespace GzipStreamExtensions.GZipTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var test = new[]
            {
                "compress",
                "resources/workingset03.pdf",
                "resources/workingset03.pdf.gz",
            };

            Bootstrapper.Start(test);
        }
    }
}
