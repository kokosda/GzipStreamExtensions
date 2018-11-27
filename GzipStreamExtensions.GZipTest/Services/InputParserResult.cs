using GzipStreamExtensions.GZipTest.Enums;

namespace GzipStreamExtensions.GZipTest.Services
{
    public class InputParserResult
    {
        public string SourceFilePath { get; set; }
        public string TargetFilePath { get; set; }
        public FileOperationsEnum FileOperation { get; set; }
    }
}
