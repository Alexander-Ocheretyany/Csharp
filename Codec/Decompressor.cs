using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Codec
{
    class Decompressor : Operation, IRunnable
    {
        private int numOfParts;

        public Decompressor(string inputFileName, string outputFileName) : base("decompress", inputFileName, outputFileName)
        {
            numOfParts = 0;
        }

        /// <summary>
        /// Decompression algorithm
        /// </summary>
        public void Run()
        {
            PrintInfo();

            using (FileStream stream = File.Open(inputFileName, FileMode.Open))
            {
                // Get footer length
                if (stream.Length > 8)
                {
                    stream.Seek(-8, SeekOrigin.End);
                }
                else
                {
                    Error("This file was not compressed by the compressor", true);
                }
                byte[] lengthB = new byte[8];
                stream.Read(lengthB, 0, lengthB.Length);
                Array.Reverse(lengthB, 0, lengthB.Length);
                long footerLength = BitConverter.ToInt64(lengthB, 0);
                // -----------------

                // Get the footer archive
                MemoryStream archiveStream = new MemoryStream();
                if (stream.Length > footerLength)
                {
                    stream.Seek(-8 - footerLength, SeekOrigin.End); // Set correct position
                    stream.CopyTo(archiveStream);
                    archiveStream.SetLength(Math.Max(0, archiveStream.Length - 8)); // Remove the size of the footer
                    archiveStream.Position = 0;
                }
                else
                {
                    Error("This file was not compressed by the compressor", true);
                }

                MemoryStream partsStream = new MemoryStream();
                try
                {
                    using (GZipStream decompressionStream = new GZipStream(archiveStream, CompressionMode.Decompress, true))
                    {
                        decompressionStream.CopyTo(partsStream);
                    }
                    partsStream.Position = 0;
                    archiveStream.Close();
                }
                catch
                {
                    Error("This file was not compressed by the compressor", true);
                }
                stream.Position = 0; // Go to the beginning

                // BUILD A DICTIONARY
                Dictionary<int, int> dictionary = new Dictionary<int, int>();
                LinkedList<int> order = new LinkedList<int>();

                using (StreamReader reader = new StreamReader(partsStream))
                {
                    string line = null;
                    while ((line = reader.ReadLine()) != null)
                    {
                        ++numOfParts;
                        int index = 0;
                        int size = 0;

                        Parse(line, ref index, ref size);

                        order.AddLast(index);
                        dictionary.Add(index, size);
                    }

                    using (FileStream outputStream = File.Create(outputFileName + extension))
                    {
                        for (int j = 0; j < dictionary.Count; ++j)
                        {
                            stream.Position = GetOffset(j, ref order, ref dictionary);
                            dictionary.TryGetValue(j, out int length);
                            byte[] block = new byte[length];
                            stream.Read(block, 0, length);

                            using (MemoryStream tmpStream = new MemoryStream(length))
                            {
                                tmpStream.Write(block, 0, length);
                                tmpStream.Position = 0;

                                using (GZipStream decompressor = new GZipStream(tmpStream, CompressionMode.Decompress, true))
                                {
                                    decompressor.CopyTo(outputStream);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parses one line of a footer file
        /// </summary>
        /// <param name="line">String to parse</param>
        /// <param name="index">Refrence to the part index</param>
        /// <param name="size">Reference to the part size</param>
        private void Parse(string line, ref int index, ref int size)
        {
            try
            {
                StringBuilder name = new StringBuilder();
                StringBuilder length = new StringBuilder();
                int i = 0;
                while (i < line.Length && line[i] != ' ')
                {
                    name.Append(line[i++]);
                }
                ++i;
                while (i < line.Length)
                {
                    length.Append(line[i++]);
                }

                if (int.TryParse(name.ToString(), out int ind) && int.TryParse(length.ToString(), out int tmp))
                {
                    index = ind;
                    size = tmp;
                }
                else
                {
                    throw new Exception();
                }
            }
            catch
            {
                index = -1;
                size = -1;
                Error("Wrong format!", true);
            }
        }

        /// <summary>
        /// Returns the offset of a part in the input file
        /// </summary>
        /// <param name="element">Index of an element</param>
        /// <param name="order">Reference to an order list</param>
        /// <param name="dictionary">Reference to a dictionary [index, size] </param>
        /// <returns>The offset of a part in the input file</returns>  
        private long GetOffset(int element, ref LinkedList<int> order, ref Dictionary<int, int> dictionary)
        {
            long offset = 0;

            foreach (int i in order)
            {
                if(i == element)
                {
                    break;
                } else
                {
                    dictionary.TryGetValue(i, out int tmp);
                    offset += tmp;
                }
            }

            return offset;
        }
    }
}