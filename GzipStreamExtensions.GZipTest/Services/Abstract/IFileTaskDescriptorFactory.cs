using GzipStreamExtensions.GZipTest.Facilities;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileTaskDescriptorFactory
    {
        ResponseContainer<FileTaskDescriptor> GetByInputParserResult(InputParserResult inputParserResult);
    }
}
