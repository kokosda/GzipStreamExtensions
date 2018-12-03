using GzipStreamExtensions.GZipTest.Artifacts;

namespace GzipStreamExtensions.GZipTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var test = new[]
            {
                "decompress",
                "resources/workingset03.pdf.gz",
                "resources/workingset03.pdf.gz",
            };

            SampleAlgorithms.DecompressReusably(test[2]);

            //Bootstrapper.Start(test);
        }
    }
}
