using GzipStreamExtensions.GZipTest.Facilities;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationsManager
    {
        ResponseContainer RunByFileTaskDescriptor(FileTaskDescriptor fileTaskDescriptor);
    }
}
