using System.IO;

namespace GzipStreamExtensions.GZipTest.Services
{
    public class FileOperationStrategyParameters<TSourceStream, TOperationStram> 
        where TSourceStream: Stream
        where TOperationStram : Stream
    {
        public TSourceStream SourceStream { get; set; }
        public TOperationStram OperationStream { get; set; }
        public byte[] Buffer { get; set; }
        public int BufferSize { get; set; }
        public int Offset { get; set; }
    }
}

