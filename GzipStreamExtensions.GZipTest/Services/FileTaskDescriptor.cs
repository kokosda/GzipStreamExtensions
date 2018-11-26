namespace GzipStreamExtensions.GZipTest.Services
{
    public class FileTaskDescriptor
    {
        public long FileLength { get; set; }
        public int ReadBufferSize { get; set; }
        public string SourceFilePath { get; set; }
        public string TargetFilePath { get; set; }
        public IFileOperationStrategy FileOperationStrategy { get; set; }
    }
}
