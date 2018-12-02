using GzipStreamExtensions.GZipTest.Enums;
using GzipStreamExtensions.GZipTest.Facilities;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationStrategyFactory<TSourceStream, TOperationStream> 
        where TSourceStream: Stream
        where TOperationStream: Stream
    {
        ResponseContainer<IFileOperationStrategy<TSourceStream, TOperationStream>> GetByFileOperation(FileOperationsEnum fileOperation);
    }
}
