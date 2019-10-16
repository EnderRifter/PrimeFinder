using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrimeNumberFinder
{
    internal enum PrintTypes { Normal, Fast, Fancy }

    internal class AsyncPrimeFinder
    {
        private PrimeSolver solver = new PrimeSolver();
        private PrimeWriter writer = new PrimeWriter(@".\primes.txt");
        private PrimeReader reader = new PrimeReader(@".\primes.txt");
        private Thread solvingThread;
        private Stopwatch thousandTimer = Stopwatch.StartNew();
        private Stopwatch millionTimer = Stopwatch.StartNew();

        internal AsyncPrimeFinder(PrintTypes printType, bool loadPrimes)
        {
            if (loadPrimes)
            {
                LoadPrimes();
            }

            switch (printType)
            {
                case PrintTypes.Normal:
                    solver.TenThousandthPrime += PrintTenThousandth;
                    solver.MillionthPrime += PrintMillionth;
                    break;

                case PrintTypes.Fast:
                    solver.MillionthPrime += PrintMillionth;
                    break;

                case PrintTypes.Fancy:
                    solver.HundredThousandthPrime += (uint p, uint pCount) => Console.Write('*');
                    solver.MillionthPrime += PrintMillionthFancy;
                    break;

                default:
                    break;
            }

            solvingThread = new Thread(new ThreadStart(solver.Solve));
        }

        private void LoadPrimes()
        {
            solver.Primes = reader.ReadPrimes();
        }

        public void Start()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("Finding Primes!");
            Console.WriteLine("========================================");
            thousandTimer.Restart();
            millionTimer.Restart();
            solvingThread.Start();
        }

        public void Stop()
        {
            List<uint> allPrimes = solver.Stop();
            writer.SavePrimes(allPrimes);
            solvingThread.Abort();
        }

        internal void PrintTenThousandth(uint prime, uint primeCount)
        {
            Console.WriteLine("{0:000000000000} | {1:000000000} | {2:000000000}", prime, primeCount, thousandTimer.ElapsedMilliseconds);
            thousandTimer.Restart();
        }

        internal void PrintMillionth(uint prime, uint primeCount)
        {
            Console.WriteLine(
                "The {0} millionth prime is {1}, found in {2}s",
                primeCount / 1000000,
                prime,
                Math.Round(millionTimer.Elapsed.TotalSeconds)
                );
            millionTimer.Restart();
        }

        internal void PrintMillionthFancy(uint prime, uint primeCount)
        {
            Console.WriteLine(
                "\nThe {0} millionth prime is {1}, found in {2}s",
                primeCount / 1000000,
                prime,
                Math.Round(millionTimer.Elapsed.TotalSeconds)
                );
            millionTimer.Restart();
        }
    }
}