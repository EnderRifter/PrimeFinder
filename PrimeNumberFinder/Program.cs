using System;

namespace PrimeNumberFinder
{
    internal class Program
    {
        private static string filePath = @".\primes.txt";

        private static void Main(string[] args)
        {
            /*
            Console.WriteLine("Read in previous prime list? [Y/N] ");
            bool loadPrimes = false;
            if (Console.ReadKey().Key == ConsoleKey.Y)
            {
                loadPrimes = true;
            }
            Console.WriteLine();
            */

            AsyncPrimeFinder p = new AsyncPrimeFinder(PrintTypes.Fast, false);
            p.Start();
            Console.ReadLine();
            p.Stop();
            Console.ReadLine();

            /*
            Console.WriteLine("Multithreaded");
            MultithreadedPrimeFinder m = new MultithreadedPrimeFinder(PrintTypes.Fancy, 1_000_000, 100_000, 1);
            m.Start();
            Console.ReadLine();
            */

            /*
            PrimeReader reader = new PrimeReader(filePath);
            List<uint> primes = reader.ReadPrimes();
            Console.WriteLine(primes.Count);
            Console.ReadLine();
            for (int i = 0; i < primes.Count; i++)
            {
                Console.WriteLine("{0:0000000000} - {1:0000000000}", primes[i], i);
            }
            Console.WriteLine(primes.Count);
            Console.ReadLine();
            */
        }
    }
}