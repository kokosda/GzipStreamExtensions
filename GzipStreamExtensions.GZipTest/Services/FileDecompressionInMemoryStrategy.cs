﻿using GzipStreamExtensions.GZipTest.Services.Abstract;
using System;
using System.IO;
using System.IO.Compression;

namespace GzipStreamExtensions.GZipTest.Services
{
    internal sealed class FileDecompressionInMemoryStrategy : IFileOperationStrategy<MemoryStream, GZipStream>
    {
        public FileOperationStrategyParameters<MemoryStream, GZipStream> Parameters { get; private set; }

        public FileDecompressionInMemoryStrategy(FileOperationStrategyParameters<MemoryStream, GZipStream> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            Parameters = parameters;
        }

        public void InitializeOperationStream(MemoryStream sourceStream)
        {
            if (sourceStream == null)
                throw new ArgumentNullException(nameof(sourceStream));

            Parameters.SourceStream = sourceStream;
            Parameters.OperationStream = new GZipStream(Parameters.SourceStream, CompressionMode.Decompress, leaveOpen: true);
        }

        public byte[] Read2(byte[] buffer, int offset, int bufferSize, bool shouldDisposeOperationStream)
        {
            byte[] temp = new byte[checked(Parameters.BufferSize * 10)];
            var bytesRead = Parameters.OperationStream.Read(temp, 0, temp.Length);

            if (shouldDisposeOperationStream)
                Parameters.OperationStream.Dispose();

            var result = new byte[bytesRead];
            Array.Copy(temp, result, result.Length);
            return result;
        }

        public byte[] Read(byte[] buffer, int offset, int bufferSize)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            byte[] result;
            byte[] temp = new byte[checked(bufferSize * 10)];
            int bytesRead;

            using (var ms = new MemoryStream(buffer, offset, bufferSize))
            {
                using (var gzipStream = new GZipStream(ms, CompressionMode.Decompress, leaveOpen: true))
                {
                    bytesRead = gzipStream.Read(temp, 0, temp.Length);
                }

                result = new byte[bytesRead];
                Array.Copy(temp, result, result.Length);
            }

            return result;
        }

        public void Write(FileStream targetFileStream, byte[] buffer, int bytesCountToWrite)
        {
            targetFileStream.Write(buffer, 0, bytesCountToWrite);
        }

        public void Dispose()
        {
            Parameters.OperationStream.Dispose();
        }
    }
}