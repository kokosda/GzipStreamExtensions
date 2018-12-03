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
                TargetStream = new FileStream(targetFilePath, FileMode.Append, FileAccess.Write),
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
            
            mutableParameters.BytesRead = immutableParameters.OperationStream.Read(mutableParameters.Buffer, mutableParameters.Offset, mutableParameters.BufferSize);

            if (mutableParameters.BytesRead == 0)
                mutableParameters.IsCompleted = true;
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
