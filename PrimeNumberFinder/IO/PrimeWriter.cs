using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrimeNumberFinder
{
    public class PrimeWriter
    {
        private StreamWriter writer;
        private string FilePath { get; set; }

        public PrimeWriter(string filePath)
        {
            FilePath = filePath;
        }

        public void SavePrimes(List<uint> primesList)
        {
            try
            {
                writer = new StreamWriter(File.Open(FilePath, FileMode.OpenOrCreate));
                for (int i = 0; i < primesList.Count; i++)
                {
                    writer.WriteLine(primesList[i]);
                }
            }
            finally
            {
                Console.WriteLine("Saved primes to list at {0}", FilePath);
                writer.Close();
            }
        }
    }
}
