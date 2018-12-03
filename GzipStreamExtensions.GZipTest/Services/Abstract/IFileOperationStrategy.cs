using System;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationStrategy
    {
        FileOperationStrategyImmutableParameters GetImmutableParameters(string sourceFilePath, string targetFilePath);
        void PerformOperation(FileOperationStrategyImmutableParameters immutableParameters, FileOperationStrategyMutableParameters mutableParameters);
        void FlushBytes(FileOperationStrategyImmutableParameters immutableParameters, FileOperationStrategyMutableParameters mutableParameters);
    }
}
