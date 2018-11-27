using GzipStreamExtensions.GZipTest.Enums;
using GzipStreamExtensions.GZipTest.Facilities;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationStrategyFactory
    {
        ResponseContainer<IFileOperationStrategy> GetByFileOperation(FileOperationsEnum fileOperation);
    }
}
