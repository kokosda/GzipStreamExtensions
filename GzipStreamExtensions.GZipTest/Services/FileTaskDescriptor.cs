using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;

namespace GzipStreamExtensions.GZipTest.Services
{
    public class FileTaskDescriptor : IDisposable
    {
        public long FileLength { get; set; }
        public string SourceFilePath { get; set; }
        public string TargetFilePath { get; set; }
        public FileOperationStrategyImmutableParameters FileOperationStrategyParameters { get; set; }
        public IFileOperationStrategy FileOperationStrategy { get; set; }

        public void Dispose()
        {
            if (FileOperationStrategyParameters != null)
            {
                FileOperationStrategyParameters.SourceStream.Dispose();
                FileOperationStrategyParameters.TargetStream.Dispose();
                FileOperationStrategyParameters.OperationStream.Dispose();
            }
        }
    }
}
