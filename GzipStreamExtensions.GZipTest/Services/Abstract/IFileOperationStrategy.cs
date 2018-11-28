using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationStrategy
    {
        byte[] Read(byte[] buffer, int offset, int bufferSize);
        byte[] Read(FileStream sourceFileStream, long seekPoint, int readBufferSize);
        void Write(FileStream targetFileStream, byte[] buffer, int bytesCountToWrite);
    }
}
