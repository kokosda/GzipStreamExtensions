using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Artifacts
{
    public static class SampleAlgorithms
    {
        public static void CompressReusably(string path)
        {
            using (Stream fs = File.OpenRead(path))
            using (Stream fd = File.Create(path + ".gz"))
            using (Stream csStream = new GZipStream(fd, CompressionMode.Compress))
            {
                byte[] buffer = new byte[1024];
                int nRead;

                while ((nRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    csStream.Write(buffer, 0, nRead);
                }
            }
        }

        public static void DecompressReusably(string gzPath)
        {
            var path = gzPath.Replace(".gz", string.Empty);

            using (Stream fd = File.Create(path))
            using (Stream fs = File.OpenRead(gzPath))
            using (Stream csStream = new GZipStream(fs, CompressionMode.Decompress))
            {
                byte[] buffer = new byte[1024];
                int nRead;

                while ((nRead = csStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fd.Write(buffer, 0, nRead);
                }
            }
        }
    }
}
