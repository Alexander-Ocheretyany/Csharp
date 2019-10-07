using Microsoft.VisualBasic.Devices;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace Codec
{
    class Compressor : Operation, IRunnable
    {
        private readonly MemoryStream[] streams;
        private readonly int[] indexes;
        private int headPointer;
        private int currentPointer;
        private readonly Semaphore semaphore;
        private int numOfThreads;
        private readonly Object threadLock = new Object();
        private readonly Object streamLock = new Object();
        private bool read, written;
        private readonly int limit;

        public Compressor(string inputFileName, string outputFileName) : base("compress", inputFileName, outputFileName)
        {
            semaphore = new Semaphore(numOfCores - 2, numOfCores - 2);
            read = false;
            written = false;
            ComputerInfo info = new ComputerInfo();
            limit = Math.Min(1000, (int)(info.AvailablePhysicalMemory * 0.1)); // 1000 or 10% of available RAM (smaller value wins)
            streams = new MemoryStream[limit];
            indexes = new int[limit];
            currentPointer = 0;
            headPointer = 0;
        }

        public void Run()
        {
            PrintInfo();

            Thread reader = new Thread(() => Split());
            reader.Start();

            Thread writer = new Thread(() => Pack());
            writer.Priority = ThreadPriority.Highest;
            writer.Start();

            while (!read || !written) { }
        }

        /// <summary>
        /// Splits the input file into parts and compresses them
        /// </summary>
        private void Split()
        {
            using (FileStream inputStream = new FileStream(inputFileName, FileMode.Open))
            {
                int part = 0;
                byte[] block = new byte[blockSize];
                int blockCount = 0;
                int halfLimit = limit / 2;
                int difference;

                while ((blockCount = inputStream.Read(block, 0, block.Length)) > 0)
                {
                    if(blockCount < blockSize) // If we reached the last block
                    {
                        Array.Resize(ref block, blockCount);
                    }

                    int tmpIndex = part++;
                    byte[] tmpBlock = block;

                    Thread compress = new Thread(() => CompressBlock(ref tmpBlock, tmpIndex));
                    ChangeNumberOfThreads(true);
                    compress.Start();

                    block = new byte[blockSize];

                    difference = currentPointer >= headPointer ? currentPointer - headPointer : limit - headPointer + currentPointer;
                    while (difference >= halfLimit) {
                        difference = currentPointer >= headPointer ? currentPointer - headPointer : limit - headPointer + currentPointer;
                    }
                }

                while (numOfThreads != 0) { }
           
                read = true; // Reading is done
            }
        }

        /// <summary>
        /// Compresses a block of bytes and stores it in the buffer
        /// </summary>
        /// <param name="block">Reference to a block to be compressed</param>
        /// <param name="index">Index of the block to be compressed</param>
        private void CompressBlock(ref byte[] block, int index)
        {
            semaphore.WaitOne();

            MemoryStream stream = new MemoryStream();
            using (GZipStream compressor = new GZipStream(stream, CompressionMode.Compress, true))
            {
                compressor.Write(block, 0, block.Length);
            }
            stream.Position = 0;

            lock (streamLock)
            {
                streams[currentPointer] = stream;
                indexes[currentPointer++] = index;
                if (currentPointer >= limit)
                {
                    currentPointer %= limit;
                }
            }

            semaphore.Release();

            ChangeNumberOfThreads(false);
        }

        /// <summary>
        /// (Sync) Changes the number of currently running compression threads
        /// </summary>
        /// <param name="increase">True to increment value, false otherwise</param>
        private void ChangeNumberOfThreads(bool increase)
        {
            lock (threadLock)
            {
                if (increase)
                {
                    ++numOfThreads;
                }
                else
                {
                    --numOfThreads;
                }
            }
        }

        /// <summary>
        /// Packs compressed parts of the input file into the output file
        /// </summary>
        private void Pack()
        {
            using (Stream outStream = new FileStream(outputFileName + extension + ".gz", FileMode.Create, FileAccess.Write))
            {
                MemoryStream stream = null;
                int index = 0;

                using (MemoryStream partsStream = new MemoryStream())
                {
                    while (true)
                    {
                        if (read && headPointer == currentPointer)
                        {
                            break;
                        }

                        while (headPointer == currentPointer) { } // Wait

                        // Read next compressed block
                        stream = streams[headPointer];
                        index = indexes[headPointer++];
                        if (headPointer >= limit)
                        {
                            headPointer %= limit;
                        }
                        // --------------------------

                        stream.CopyTo(outStream);
                        byte[] bytes = Encoding.ASCII.GetBytes(index + " " + stream.Length + '\n');
                        partsStream.Write(bytes, 0, bytes.Length);
                    }

                    partsStream.Position = 0;
                    MemoryStream footerStream = new MemoryStream();
                    using (GZipStream compressor = new GZipStream(footerStream, CompressionMode.Compress, true))
                    {
                        partsStream.CopyTo(compressor);
                    }
                    footerStream.Position = 0;
                    footerStream.CopyTo(outStream);

                    byte[] bits = BitConverter.GetBytes(footerStream.Length);
                    for (long j = 0; j < bits.Length; j++)
                    {
                        outStream.WriteByte(bits[bits.Length - 1 - j]);
                    }
                }
                written = true;
            }
        }
    }
}