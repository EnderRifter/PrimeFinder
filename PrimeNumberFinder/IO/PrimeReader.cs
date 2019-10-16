using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrimeNumberFinder
{
    public class PrimeReader
    {
        private StreamReader reader;
        private string FilePath { get; set; }
        private List<uint> primes = new List<uint>();

        public PrimeReader(string filePath)
        {
            FilePath = filePath;
        }

        public List<uint> ReadPrimes()
        {
            try
            {
                reader = new StreamReader(new FileStream(FilePath, FileMode.OpenOrCreate));
                string number;
                while (true)
                {
                    number = reader.ReadLine();
                    primes.Add(Convert.ToUInt32(number));

                    if (reader.EndOfStream)
                    {
                        break;
                    }
                }
                Console.WriteLine("Read primes from list at {0}", FilePath);
            }
            catch (OutOfMemoryException)
            {
                Console.WriteLine("OutOfMemoryException. File too large. Read failed");
            }
            finally
            {
                if (primes.Count == 0)
                {
                    primes.Add(2);
                    primes.Add(3);
                }
                reader.Close();
            }

            return primes;
        }
    }
}