using GzipStreamExtensions.GZipTest.Enums;
using GzipStreamExtensions.GZipTest.Facilities;
using GzipStreamExtensions.GZipTest.Services.Abstract;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileOperationStrategyFactory : IFileOperationStrategyFactory
    {
        public ResponseContainer<IFileOperationStrategy> GetByFileOperation(FileOperationsEnum fileOperation)
        {
            var result = new ResponseContainer<IFileOperationStrategy>(success: true);

            if (fileOperation == FileOperationsEnum.None)
            {
                result.AddErrorMessage($"File operation {fileOperation} is not supported.");
                return result;
            }

            switch(fileOperation)
            {
                case FileOperationsEnum.Compression:
                    result.SetSuccessValue(new FileCompressionStrategy());
                    break;
                case FileOperationsEnum.Decompression:
                    break;
            }

            return result;
        }
    }
}
