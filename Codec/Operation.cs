using System;
using System.IO;

namespace Codec
{
    /// <summary>
    /// Abstract class for compressor and decompressor, provides them with basic functionality and common variables
    /// </summary>
    abstract class Operation
    {
        private readonly string operation; // Name of the operation
        public readonly string inputFileName;
        public readonly string outputFileName;
        public readonly string extension; // File extension
        public readonly int blockSize = 1024 * 1024; // 1 MB block
        public readonly int numOfCores; // Number of cores of the processor

        public Operation(string operation, string inputFileName, string outputFileName)
        {
            this.operation = operation;
            this.inputFileName = inputFileName;
            this.outputFileName = outputFileName;

            try
            {
                if (operation.Equals("compress")) extension = Path.GetExtension(inputFileName);
                else extension = Path.GetExtension(Path.GetFileNameWithoutExtension(inputFileName));
            }
            catch
            {
                extension = "";
            }

            // Check existence of the input and the output file
            if (!File.Exists(inputFileName))
            {
                Error("File \"" + inputFileName + "\" does not exist!", true);
            }

            if ((operation.Equals("compress") && File.Exists(outputFileName + extension + ".gz")) ||
                operation.Equals("decompress") && File.Exists(outputFileName + extension))
            {
                Error("You are trying to override the output file; prohibited!", true);
            }
            // ------------------------------------------------

            numOfCores = 0;

            try
            {
                numOfCores = Environment.ProcessorCount;
            }
            catch
            {
                numOfCores = 1; // If something went wrong use just only one core
                Error("Unable to detect the number of cores; only 1 thread will be used!", false);
            }
        }

        /// <summary>
        /// Prints basic information
        /// </summary>
        public void PrintInfo()
        {
            Console.WriteLine("Command: " + operation);
            Console.WriteLine("Input file name: " + inputFileName);
            Console.WriteLine("Output file name: " + outputFileName + '\n');
        }

        /// <summary>
        /// Prints error message and finishes the program if the error is critical
        /// </summary>
        /// <param name="msg">Message to be written</param>
        /// <param name="isCritical">Flag; if true - finishes the program after message printing</param>
        public static void Error(string msg, bool isCritical)
        {
            Console.WriteLine(msg);
            if (isCritical)
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }
    }
}