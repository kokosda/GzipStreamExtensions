using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationStrategy
    {
        byte[] Read(byte[] buffer, int offset, int bufferSize);
        void Write(FileStream targetFileStream, byte[] buffer, int bytesCountToWrite);
    }
}
