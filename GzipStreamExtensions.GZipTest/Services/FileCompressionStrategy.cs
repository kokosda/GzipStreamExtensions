using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;
using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileCompressionStrategy : IFileOperationStrategy
    {
        public byte[] Read(FileStream sourceFileStream, long seekPoint, int readBufferSize)
        {
            sourceFileStream.Seek(seekPoint, SeekOrigin.Begin);
            var readBuffer = new byte[readBufferSize];
            var bytesRead = sourceFileStream.Read(readBuffer, 0, readBufferSize);
            byte[] result;

            using (var ms = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(ms, CompressionMode.Compress))
                {
                    gzipStream.Write(readBuffer, 0, bytesRead);
                }

                result = ms.ToArray();
            }

            return result;
        }

        public byte[] Read(byte[] buffer, int offset, int bufferSize)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            byte[] result;

            using (var ms = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(ms, CompressionMode.Compress))
                {
                    gzipStream.Write(buffer, offset, bufferSize);
                }

                result = ms.ToArray();
            }

            return result;
        }

        public void Write(FileStream targetFileStream, byte[] buffer, int bytesCountToWrite)
        {
            targetFileStream.Write(buffer, 0, bytesCountToWrite);
        }
    }
}
