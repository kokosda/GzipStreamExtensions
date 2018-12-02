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
                "resources/workingset01.mp4",
            };

            SampleAlgorithms.CompressReusably(test[2]);

            //Bootstrapper.Start(test);
        }
    }
}
