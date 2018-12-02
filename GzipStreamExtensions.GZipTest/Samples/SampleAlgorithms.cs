using System;
using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Artifacts
{
    public static class SampleAlgorithms
    {
        public static void Compress(string path)
        {
            using (var fileReadStream = File.OpenRead(path))
            {
                if (!fileReadStream.CanRead)
                    throw new InvalidOperationException("File " + path + " is not readable.");

                if (!fileReadStream.CanSeek)
                    throw new InvalidOperationException("File " + path + " is not seekable.");

                Console.WriteLine("Total bytes to read and compress {0}", fileReadStream.Length);

                var gzPath = path + ".gz";

                if (File.Exists(gzPath))
                    File.Delete(gzPath);

                var bufferSize = 1 * 1024 * 1024;
                byte[] readBuffer;
                var readOffset = 0L;
                var bytesRead = 0;
                var bytesWrote = 0;

                do
                {
                    fileReadStream.Seek(readOffset, SeekOrigin.Begin);
                    readBuffer = new byte[bufferSize];
                    bytesRead = fileReadStream.Read(readBuffer, 0, bufferSize);
                    readOffset += bytesRead;

                    byte[] compressedBytes;
                    var ms = new MemoryStream();

                    using (var gzipStream = new GZipStream(ms, CompressionMode.Compress))
                    {
                        gzipStream.Write(readBuffer, 0, bytesRead);
                    }

                    compressedBytes = ms.ToArray();
                    ms.Dispose();

                    using (var gzFileStream = new FileStream(gzPath, FileMode.Append, FileAccess.Write))
                    {
                        gzFileStream.Write(compressedBytes, 0, compressedBytes.Length);
                        bytesWrote += compressedBytes.Length;
                    }

                    Console.WriteLine("Bytes read: {0}", readOffset);
                }
                while (readOffset != fileReadStream.Length);

                Console.WriteLine("Original size {0}, compressed size {1} in bytes.", readOffset, bytesWrote);
            }
        }

        public static void CompressReusably(string path)
        {
            using (var fileReadStream = File.OpenRead(path))
            {
                if (!fileReadStream.CanRead)
                    throw new InvalidOperationException("File " + path + " is not readable.");

                if (!fileReadStream.CanSeek)
                    throw new InvalidOperationException("File " + path + " is not seekable.");

                Console.WriteLine("Total bytes to read and compress {0}", fileReadStream.Length);

                var gzPath = path + ".gz";

                if (File.Exists(gzPath))
                    File.Delete(gzPath);

                var bufferSize = 1 * 1024 * 1024;
                byte[] readBuffer;
                var readOffset = 0L;
                var bytesRead = 0;
                var bytesWrote = 0;
                var ms = new MemoryStream();
                var gZipStream = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true);
                var i = 0;
                do
                {
                    if (i > 0 && i % 5 == 0)
                    {
                        ms.Flush();
                        gZipStream.Flush();
                    }

                    fileReadStream.Seek(readOffset, SeekOrigin.Begin);
                    readBuffer = new byte[bufferSize];
                    bytesRead = fileReadStream.Read(readBuffer, 0, bufferSize);
                    readOffset += bytesRead;

                    byte[] compressedBytes;

                    gZipStream.Write(readBuffer, 0, bytesRead);

                    if (i > 0 && i % 5 == 0 || readOffset == fileReadStream.Length)
                    {
                        if (readOffset == fileReadStream.Length)
                            gZipStream.Dispose();

                        compressedBytes = ms.ToArray();

                        using (var gzFileStream = new FileStream(gzPath, FileMode.Append, FileAccess.Write))
                        {
                            gzFileStream.Write(compressedBytes, 0, compressedBytes.Length);
                            bytesWrote += compressedBytes.Length;
                        }
                    }

                    Console.WriteLine("Bytes read: {0}", readOffset);
                    i++;
                }
                while (readOffset != fileReadStream.Length);

                Console.WriteLine("Original size {0}, compressed size {1} in bytes.", readOffset, bytesWrote);
            }
        }
    }
}
