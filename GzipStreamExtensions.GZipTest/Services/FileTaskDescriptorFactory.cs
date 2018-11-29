using GzipStreamExtensions.GZipTest.Facilities;
using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileTaskDescriptorFactory : IFileTaskDescriptorFactory
    {
        private readonly IFileOperationStrategyFactory fileOperationStrategyFactory;
        private readonly ILog log;

        public FileTaskDescriptorFactory(IFileOperationStrategyFactory fileOperationStrategyFactory, ILog log)
        {
            this.fileOperationStrategyFactory = fileOperationStrategyFactory;
            this.log = log;
        }

        public ResponseContainer<FileTaskDescriptor> GetByInputParserResult(InputParserResult inputParserResult)
        {
            var result = new ResponseContainer<FileTaskDescriptor>(success: true);

            if (inputParserResult == null)
            {
                result.AddErrorMessage($"{nameof(inputParserResult)} is not defined.");
                return result;
            }

            var fileOperationStrategyResponseContainer = fileOperationStrategyFactory.GetByFileOperation(inputParserResult.FileOperation);
            result.Join(fileOperationStrategyResponseContainer);

            var sourceFileAvailabilityResponseContainer = CheckSourceFileAvailability(inputParserResult.SourceFilePath);
            result.Join(sourceFileAvailabilityResponseContainer);

            var targetFileAvailabilityResponseContainer = CheckTargetFileAvailability(inputParserResult.TargetFilePath);
            result.Join(targetFileAvailabilityResponseContainer);

            if (!result.Success)
                return result;

            var fileTaskDescriptor = new FileTaskDescriptor
            {
                FileOperationStrategy = fileOperationStrategyResponseContainer.Value,
                SourceFilePath = inputParserResult.SourceFilePath,
                FileLength = sourceFileAvailabilityResponseContainer.Value,
                TargetFilePath = inputParserResult.TargetFilePath
            };

            result.SetSuccessValue(fileTaskDescriptor);

            return result;
        }

        private ResponseContainer<long> CheckSourceFileAvailability(string filePath)
        {
            var result = new ResponseContainer<long>(success: true);

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    result.SetSuccessValue(fs.Length);
                }
            }
            catch(Exception ex)
            {
                result.AddErrorMessage($"Error while checking source file {filePath}. Message: {ex.Message}");
            }

            return result;
        }

        private ResponseContainer CheckTargetFileAvailability(string filePath)
        {
            var result = new ResponseContainer(success: true);

            try
            {
                using (var fs = File.Create(filePath))
                {
                    log.LogInfo($"File {filePath} was created.");
                }
            }
            catch (Exception ex)
            {
                result.AddErrorMessage($"Error while checking target file {filePath}. Error: {ex.Message}");
            }

            return result;
        }
    }
}
