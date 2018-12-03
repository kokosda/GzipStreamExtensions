namespace GzipStreamExtensions.GZipTest.Services
{
    public sealed class FileOperationStrategyMutableParameters
    {
        public byte[] Buffer { get; set; }
        public int Offset { get; set; }
        public int BufferSize { get; set; }
        public bool IsCompleted { get; set; }
        public int BytesRead { get; set; }
    }
}
