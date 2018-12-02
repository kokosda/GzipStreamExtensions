using System;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationStrategy<TSourceStream, TOperationStream> : IDisposable
        where TSourceStream : Stream
        where TOperationStream : Stream
    {
        FileOperationStrategyParameters<TSourceStream, TOperationStream> Parameters { get; }

        void InitializeOperationStream(TSourceStream sourceStream);
        byte[] Read(byte[] buffer, int offset, int bufferSize);
        byte[] Read2(byte[] buffer, int offset, int bufferSize, bool shouldCloseOperationStream);
        void Write(FileStream targetFileStream, byte[] buffer, int bytesCountToWrite);
    }
}
