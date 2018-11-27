using GzipStreamExtensions.GZipTest.Facilities;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileTaskDescriptorFactory
    {
        ResponseContainer<FileTaskDescriptor> GetByInputParserResult(InputParserResult inputParserResult);
    }
}
