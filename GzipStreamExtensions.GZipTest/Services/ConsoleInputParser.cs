using GzipStreamExtensions.GZipTest.Enums;
using GzipStreamExtensions.GZipTest.Facilities;
using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class ConsoleInputParser : IInputParser
    {
        public ResponseContainer<InputParserResult> Parse(string[] arguments)
        {
            var result = new ResponseContainer<InputParserResult>(success: true);
            var usageAppending = "Usage: compress/decompress [source file path] [target file path]";

            if (arguments == null)
            {
                result.AddErrorMessage($"Arguments should be passed. {usageAppending}");
                return result;
            }

            const int expectedArgumentsCount = 3;

            if (arguments.Length < expectedArgumentsCount || arguments.Length > expectedArgumentsCount)
            {
                result.AddErrorMessage($"Expected arguments count is {expectedArgumentsCount}. {usageAppending}");
                return result;
            }

            var fileOperationString = arguments[0];
            var inputParserResult = new InputParserResult();
            inputParserResult.FileOperation = GetFileOperation(fileOperationString);
            
            if (inputParserResult.FileOperation == FileOperationsEnum.None)
                result.AddErrorMessage($"File operation \"{fileOperationString}\" is not supported.");

            if (string.IsNullOrEmpty(arguments[1]))
                result.AddErrorMessage($"Source file path should be specified.");

            if (string.IsNullOrEmpty(arguments[2]))
                result.AddErrorMessage($"Target file path should be specified.");

            if (!result.Success)
            {
                result.AddMessage(usageAppending);
                return result;
            }

            inputParserResult.SourceFilePath = arguments[1];
            inputParserResult.TargetFilePath = arguments[2];

            result.SetSuccessValue(inputParserResult);
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
