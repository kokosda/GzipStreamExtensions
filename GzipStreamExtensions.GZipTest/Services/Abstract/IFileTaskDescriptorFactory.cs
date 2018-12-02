using GzipStreamExtensions.GZipTest.Facilities;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileTaskDescriptorFactory<TSourceStream, TOperationStream>
        where TSourceStream: Stream
        where TOperationStream: Stream
    {
        ResponseContainer<FileTaskDescriptor<TSourceStream, TOperationStream>> GetByInputParserResult(InputParserResult inputParserResult);
    }
}
