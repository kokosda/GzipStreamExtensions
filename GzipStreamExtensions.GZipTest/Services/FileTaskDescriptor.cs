using GzipStreamExtensions.GZipTest.Services.Abstract;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services
{
    public class FileTaskDescriptor<TSourceStream, TOperationStream>
        where TSourceStream: Stream
        where TOperationStream: Stream
    {
        public long FileLength { get; set; }
        public string SourceFilePath { get; set; }
        public string TargetFilePath { get; set; }
        public TSourceStream SourceStream { get; set; }
        public IFileOperationStrategy<TSourceStream, TOperationStream> FileOperationStrategy { get; set; }
    }
}
