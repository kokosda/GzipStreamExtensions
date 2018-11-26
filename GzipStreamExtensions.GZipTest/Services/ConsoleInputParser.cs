using GzipStreamExtensions.GZipTest.Enums;
using System;

namespace GzipStreamExtensions.GZipTest.Services
{
    public class ConsoleInputParser : IInputParser
    {
        public InputParserResult Parse(string[] arguments)
        {
            var result = new InputParserResult();
            var usageAppending = "Usage: compress/decompress [source file path] [target file path]";

            if (arguments == null)
            {
                result.Message = $"Arguments should be passed. {usageAppending}";
                return result;
            }

            const int expectedArgumentsCount = 3;

            if (arguments.Length < expectedArgumentsCount || arguments.Length > expectedArgumentsCount)
            {
                result.Message = $"Expected arguments count is {expectedArgumentsCount}. {usageAppending}";
                return result;
            }

            var fileOperationString = arguments[0];
            result.FileOperation = GetFileOperation(fileOperationString);
            
            if (result.FileOperation == FileOperationsEnum.None)
            {
                result.Message = $"File operation \"{fileOperationString}\" is not supported. {usageAppending}";
                return result;
            }

            if (string.IsNullOrEmpty(arguments[1]))
            {
                result.Message = $"Source file path should be specified. {usageAppending}";
                return result;
            }

            result.Success = true;
            return result;
        }

        private FileOperationsEnum GetFileOperation(string fileOperationString)
        {
            var result = FileOperationsEnum.None;

            if (string.Equals(fileOperationString, "compress", StringComparison.InvariantCultureIgnoreCase))
                result = FileOperationsEnum.Compression;
            else if (string.Equals(fileOperationString, "decompress", StringComparison.InvariantCultureIgnoreCase))
                result = FileOperationsEnum.Decompression;

            return result;
        }
    }
}
