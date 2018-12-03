using System.IO;

namespace GzipStreamExtensions.GZipTest.Services
{
    public sealed class FileOperationStrategyImmutableParameters
    {
        public Stream SourceStream { get; set; }
        public Stream TargetStream { get; set; }
        public Stream OperationStream { get; set; }
        public int ReadBufferSize { get; set; }
        public int ReadBufferChunks { get; set; }
        public int WriteBufferSize { get; set; }
        public int WriteBufferChunks { get; set; }
    }
}
