using System;
using System.IO;

namespace GzipStreamExtensions.GZipTest.Services.Abstract
{
    public interface IFileOperationStrategy
    {
        FileOperationStrategyImmutableParameters GetImmutableParameters(string sourceFilePath, string targetFilePath);
        void Read(FileOperationStrategyImmutableParameters immutableParameters, FileOperationStrategyMutableParameters mutableParameters);
        void Write(FileOperationStrategyImmutableParameters immutableParameters, FileOperationStrategyMutableParameters mutableParameters);
    }
}
