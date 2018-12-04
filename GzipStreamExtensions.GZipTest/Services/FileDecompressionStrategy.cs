using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;
using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileDecompressionStrategy : IFileOperationStrategy
    {
        public FileOperationStrategyImmutableParameters GetImmutableParameters(string sourceFilePath, string targetFilePath)
        {
            var result = new FileOperationStrategyImmutableParameters
            {
                SourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read),
                TargetStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write),
                ReadBufferSize = 16 * 1024 * 1024,
                ReadBufferChunks = 16,
                WriteBufferSize = 16 * 1024 * 1024,
                WriteBufferChunks = 1
            };

            result.OperationStream = new GZipStream(result.SourceStream, CompressionMode.Decompress);
            return result;
        }

        public void Read(FileOperationStrategyImmutableParameters immutableParameters, FileOperationStrategyMutableParameters mutableParameters)
        {
            if (immutableParameters == null)
                throw new ArgumentNullException(nameof(immutableParameters));

            if (mutableParameters == null)
                throw new ArgumentNullException(nameof(mutableParameters));

            var bytesRead = immutableParameters.OperationStream.Read(mutableParameters.Buffer, mutableParameters.Offset, mutableParameters.BufferSize);
            var isCompleted = bytesRead == 0 || immutableParameters.SourceStream.Position == immutableParameters.SourceStream.Length;

            mutableParameters.IsCompleted = isCompleted;
            mutableParameters.BytesRead = bytesRead;
        }

        public void Write(FileOperationStrategyImmutableParameters immutableParameters, FileOperationStrategyMutableParameters mutableParameters)
        {
            if (mutableParameters.IsCompleted)
            {
                immutableParameters.OperationStream.Dispose();
                immutableParameters.SourceStream.Dispose();
            }

            immutableParameters.TargetStream.Write(mutableParameters.Buffer, mutableParameters.Offset, mutableParameters.BufferSize);

            if (mutableParameters.IsCompleted)
                immutableParameters.TargetStream.Dispose();
        }
    }
}
