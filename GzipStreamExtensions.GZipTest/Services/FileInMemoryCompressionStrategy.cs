using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;
using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileInMemoryCompressionStrategy : IFileOperationStrategy<MemoryStream, GZipStream>
    {
        public FileOperationStrategyParameters<MemoryStream, GZipStream> Parameters { get; private set; }

        public FileInMemoryCompressionStrategy(FileOperationStrategyParameters<MemoryStream, GZipStream> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            Parameters = parameters;
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

        public byte[] Read()
        {            
            Parameters.OperationStream.Write(Parameters.Buffer, Parameters.Offset, Parameters.BufferSize);
            var result = Parameters.SourceStream.ToArray();
            return result;
        }

        public void Write(FileStream targetFileStream, byte[] buffer, int bytesCountToWrite)
        {
            targetFileStream.Write(buffer, 0, bytesCountToWrite);
        }
    }
}
