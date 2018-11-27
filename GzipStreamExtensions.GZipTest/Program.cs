using GzipStreamExtensions.GZipTest.Services;
using GzipStreamExtensions.GZipTest.Services.Abstract;
using GzipStreamExtensions.GZipTest.Threads;
using System;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var arguments = args;

            if (arguments == null || arguments.Length == 0)
            {
                Console.WriteLine("Usage: compress/decompress [source file path] [target file path]");
                var line = Console.ReadLine();
                arguments = line.Split(' ');
            }

            IInputParser inputParser = new ConsoleInputParser();
            var inputParserResultResponseContainer = inputParser.Parse(arguments);

            if (!inputParserResultResponseContainer.Success)
            {
                WriteMessage(inputParserResultResponseContainer.MergeMessages());
                return;
            }

            var inputParserResult = inputParserResultResponseContainer.Value;

            IFileOperationStrategyFactory fileOperationStrategyFactory = new FileOperationStrategyFactory();
            IFileTaskDescriptorFactory fileTaskDescriptorFactory = new FileTaskDescriptorFactory(fileOperationStrategyFactory);

            var fileTaskDescriptorResponseContainer = fileTaskDescriptorFactory.GetByInputParserResult(inputParserResult);

            if (!fileTaskDescriptorResponseContainer.Success)
            {
                WriteMessage(fileTaskDescriptorResponseContainer.MergeMessages());
                return;
            }

            var fileTaskDescriptor = fileTaskDescriptorResponseContainer.Value;
            IThreadStateDispatcher threadStateDispatcher = new ThreadStateDispatcher();
            IFileOperationsManager fileOperationsManager = new FileOperationsManager(threadStateDispatcher);

            var runResponseContainer = fileOperationsManager.RunByFileTaskDescriptor(fileTaskDescriptor);

            if (!runResponseContainer.Success)
            {
                WriteMessage(runResponseContainer.MergeMessages());
                return;
            }

            WriteMessage("Processed successfully.");
        }

        private static void WriteMessage(string message)
        {
            Console.WriteLine(message);
            Console.ReadKey();
        }
    }
}
