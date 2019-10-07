using System;

namespace Codec
{
    class Program
    {
        private static string command; // Command name
        private static string inputFileName;
        private static string outputFileName;
        private static IRunnable operation;

        /// <summary>
        /// Method for error printing and exiting
        /// </summary>
        /// <param name="msg">Message to be printed</param>
        private static void Error(string msg)
        {
            Console.WriteLine(msg);
            Console.WriteLine("Please, press any key...");
            Console.ReadLine();
            Environment.Exit(1);
        }

        static int Main(string[] args)
        {
            // Read command
            try
            {
                command = args[0];
                inputFileName = args[1];
                outputFileName = args[2];
            }
            catch
            {
                Error("Input error");
            }
            Console.WriteLine(); // Formatting (empty line)
            // ------------

            if (command.Equals("compress")) operation = new Compressor(inputFileName, outputFileName);
            else operation = new Decompressor(inputFileName, outputFileName);

            var watch = System.Diagnostics.Stopwatch.StartNew();

            operation.Run(); // Start operation

            watch.Stop();
            var elapsedS = (double) watch.ElapsedMilliseconds / 1000;

            Console.WriteLine("Elapsed time: " + elapsedS + " s\n");
            Console.WriteLine("Please, press any key...");
            Console.ReadLine();

            return 0;
        }
    }
}
