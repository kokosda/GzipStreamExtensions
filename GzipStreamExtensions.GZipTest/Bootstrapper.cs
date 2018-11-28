using GzipStreamExtensions.GZipTest.Services;
using GzipStreamExtensions.GZipTest.Services.Abstract;
using GzipStreamExtensions.GZipTest.Threads;
using System;

namespace GzipStreamExtensions.GZipTest
{
    internal sealed class Bootstrapper
    {
        internal static void Start(string[] arguments)
        {
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
            ILog log = new ConsoleLog();
            IFileOperationsManager fileOperationsManager = new FileOperationsManager(threadStateDispatcher, log);

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
