using GzipStreamExtensions.GZipTest.Enums;
using GzipStreamExtensions.GZipTest.Facilities;
using GzipStreamExtensions.GZipTest.Services.Abstract;
using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileOperationStrategyFactory : IFileOperationStrategyFactory<MemoryStream, GZipStream>
    {
        public ResponseContainer<IFileOperationStrategy<MemoryStream, GZipStream>> GetByFileOperation(FileOperationsEnum fileOperation)
        {
            var result = new ResponseContainer<IFileOperationStrategy<MemoryStream, GZipStream>>(success: true);

            if (fileOperation == FileOperationsEnum.None)
            {
                result.AddErrorMessage($"File operation {fileOperation} is not supported.");
                return result;
            }

            switch(fileOperation)
            {
                case FileOperationsEnum.Compression:
                    {
                        var parameters = new FileOperationStrategyParameters<MemoryStream, GZipStream>();
                        result.SetSuccessValue(new FileInMemoryCompressionStrategy(parameters));
                    }
                    break;
                case FileOperationsEnum.Decompression:
                    {
                        var parameters = new FileOperationStrategyParameters<MemoryStream, GZipStream>();
                        result.SetSuccessValue(new FileInMemoryDecompressionStrategy(parameters));
                    }
                    break;
            }

            return result;
        }
    }
}
