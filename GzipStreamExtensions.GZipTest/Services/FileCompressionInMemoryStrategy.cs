using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;
using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileCompressionInMemoryStrategy : IFileOperationStrategy<MemoryStream, GZipStream>
    {
        public FileOperationStrategyParameters<MemoryStream, GZipStream> Parameters { get; private set; }

        public FileCompressionInMemoryStrategy(FileOperationStrategyParameters<MemoryStream, GZipStream> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            Parameters = parameters;
        }

        public void InitializeOperationStream(MemoryStream sourceStream)
        {
            //if (sourceStream == null)
                //throw new ArgumentNullException(nameof(sourceStream));

            Parameters.SourceStream = new MemoryStream();
            Parameters.OperationStream = new GZipStream(Parameters.SourceStream, CompressionMode.Compress, leaveOpen: true);
        }

        public byte[] Read2(byte[] buffer, int offset, int bufferSize, bool shouldCloseOperationStream)
        {
            var result = Read(buffer, offset, bufferSize);
            return result;
            /*
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            Parameters.OperationStream.Write(buffer, offset, bufferSize);

            if (shouldCloseOperationStream)
                Parameters.OperationStream.Dispose();

            var result = Parameters.SourceStream.ToArray();
            return result;*/
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

        public void Dispose()
        {
            Parameters.OperationStream.Dispose();
        }
    }
}
