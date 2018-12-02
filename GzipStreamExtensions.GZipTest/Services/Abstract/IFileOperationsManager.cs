using GzipStreamExtensions.GZipTest.Facilities;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationsManager<TSourceStream, TOperationStream>
        where TSourceStream: Stream
        where TOperationStream: Stream
    {
        ResponseContainer RunByFileTaskDescriptor(FileTaskDescriptor<TSourceStream, TOperationStream> fileTaskDescriptor);
    }
}
