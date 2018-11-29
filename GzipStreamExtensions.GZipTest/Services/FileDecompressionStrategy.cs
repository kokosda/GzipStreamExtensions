using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;
using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileDecompressionStrategy : IFileOperationStrategy
    {
        public byte[] Read(byte[] buffer, int offset, int bufferSize)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            byte[] result;
            byte[] temp = new byte[checked(bufferSize * 10)];
            int bytesRead;

            using (var ms = new MemoryStream(buffer, offset, bufferSize))
            {
                using (var gzipStream = new GZipStream(ms, CompressionMode.Decompress))
                {
                    bytesRead = gzipStream.Read(temp, 0, temp.Length);
                }

                result = new byte[bytesRead];
                Array.Copy(temp, result, result.Length);
            }

            return result;
        }

        public void Write(FileStream targetFileStream, byte[] buffer, int bytesCountToWrite)
        {
            targetFileStream.Write(buffer, 0, bytesCountToWrite);
        }
    }
}
