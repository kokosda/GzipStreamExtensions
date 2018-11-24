using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace GzipStreamExtensions.GZipTest
{
    public static class Operator
    {
        public static void CompressAsync(string path)
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

        public static void DecompressAsync(this GZipStream gzipStream)
        {

        }
    }
}
