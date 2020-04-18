using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PrimeFinderCore
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            const int upperPrimeBound = 15485864; //1_000_000_000;
            const int segmentSize = 3_000; //10_000;

            static void Benchmark(Func<int, List<int>> sieveFunc, int upperPrimeBound)
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();
                List<int> primes = sieveFunc(upperPrimeBound);
                stopwatch.Stop();

                Console.WriteLine($"We found {primes.Count} primes below {upperPrimeBound} with {sieveFunc.Method.Name} in {stopwatch.ElapsedTicks} ticks");

                GC.Collect();
            }

            static void BenchmarkSegmented(Func<int, int, List<int>> sieveFunc, int upperPrimeBound, int segmentSize)
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();
                List<int> primes = sieveFunc(upperPrimeBound, segmentSize);
                stopwatch.Stop();

                Console.WriteLine($"We found {primes.Count} primes below {upperPrimeBound} with {sieveFunc.Method.Name} in {stopwatch.ElapsedTicks} ticks");

                GC.Collect();
            }

            static void SieveDiff(Func<int, List<int>> sieveA, Func<int, List<int>> sieveB, int upperPrimeBound)
            {
                List<int> primeListA = sieveA(upperPrimeBound);
                HashSet<int> primeSetA = new HashSet<int>(primeListA);
                GC.Collect();

                List<int> primeListB = sieveB(upperPrimeBound);
                HashSet<int> primeSetB = new HashSet<int>(primeListB);
                GC.Collect();

                Console.WriteLine($"Found {primeListA.Count} primes with sieve A, and {primeListB.Count} primes with sieve B");

                List<int> diff = new List<int>();

                foreach (int item in primeListA)
                {
                    if (primeSetB.Contains(item)) continue;

                    Console.WriteLine($"Primes list A has member {item}, which does not appear in prime list B.");
                    diff.Add(item);
                }

                foreach (int item in primeListB)
                {
                    if (primeSetA.Contains(item)) continue;

                    Console.WriteLine($"Primes list B has member {item}, which does not appear in prime list A.");
                    diff.Add(item);
                }

                if (diff.Count == 0)
                {
                    Console.WriteLine("No differences were found between the two prime lists!");
                }
            }

            Benchmark(SieveOfEratosthenes, upperPrimeBound);

            Benchmark(SieveOfEratosthenesOptimised, upperPrimeBound);

            BenchmarkSegmented(SegmentedSieveOfEratosthenes, upperPrimeBound, segmentSize);

            SieveDiff(SieveOfEratosthenesOptimised, bound => SegmentedSieveOfEratosthenes(bound, segmentSize), upperPrimeBound);

            Console.WriteLine("Goodbye World!");
        }

        private static List<int> SieveOfEratosthenes(int upperBound)
        {
            if (upperBound < 2)
            {
                throw new ArgumentException("No primes in desired range; upperBound too small!");
            }
            
            if (upperBound == 2)
            {
                return new List<int> { 2 };
            }

            List<int> primes = new List<int>();

            bool[] primalityMatrix = new bool[upperBound + 1];

            for (int i = 2; i < primalityMatrix.Length; i++)
            {
                primalityMatrix[i] = true;
            }

            primalityMatrix[0] = false;
            primalityMatrix[1] = false;

            for (int primeCandidate = 2; primeCandidate < upperBound; primeCandidate++)
            {
                if (!primalityMatrix[primeCandidate]) continue;  // ignore non-prime number

                primes.Add(primeCandidate);

                // sieve out all multiples of current candidate
                for (int i = 2 * primeCandidate; i < primalityMatrix.Length; i += primeCandidate)
                {
                    primalityMatrix[i] = false;
                }
            }

            return primes;
        }

        private static List<int> SieveOfEratosthenesOptimised(int upperBound)
        {
            if (upperBound < 2)
            {
                throw new ArgumentException("No primes in desired range; upperBound too small!");
            }

            if (upperBound == 2)
            {
                return new List<int> { 2 };
            }

            bool[] primalityMatrix = new bool[upperBound + 1];

            for (int i = 3; i < primalityMatrix.Length; i += 2)
            {
                primalityMatrix[i] = i % 2 != 0;
            }

            primalityMatrix[0] = false;
            primalityMatrix[1] = false;
            primalityMatrix[2] = true;

            List<int> primes = new List<int>() { 2 };

            int primeCandidate;
            for (primeCandidate = 3; primeCandidate < Math.Sqrt(upperBound); primeCandidate++)
            {
                if (!primalityMatrix[primeCandidate]) continue;  // skip non-prime numbers

                primes.Add(primeCandidate);

                // strike out any non-prime multiples of current candidate
                for (int i = primeCandidate * primeCandidate; i < upperBound; i += 2 * primeCandidate)
                {
                    primalityMatrix[i] = false;
                }
            }

            if (primeCandidate % 2 == 0)
            {
                primeCandidate++;
            }

            for (int n = primeCandidate; n < upperBound; n += 2)
            {
                if (primalityMatrix[n])
                {
                    primes.Add(n);
                }
            }

            return primes;
        }

        private static List<int> SegmentedSieveOfEratosthenes(int upperBound, int segmentSize)
        {
            if (upperBound < 2)
            {
                throw new ArgumentException("No primes in desired range; upperBound too small!");
            }

            if (upperBound == 2)
            {
                return new List<int> { 2 };
            }

            if (segmentSize > Math.Sqrt(upperBound))
            {
                throw new ArgumentException("The segment size cannot be greater than sqrt(upperBound)!");
            }

            List<int> primes = new List<int>();


            int segmentCount = upperBound / segmentSize + (upperBound % segmentSize == 0 ? 0 : 1);
            int currentLowerBound = 3, currentUpperBound = segmentSize;  // current segment bounds
            
            /* Sieving root segment using regular sieve of Eratosthenes */
            bool[] segmentPrimalityMatrix = new bool[segmentSize];

            for (int i = 3; i < segmentPrimalityMatrix.Length; i += 2)
            {
                segmentPrimalityMatrix[i] = i % 2 != 0;
            }

            segmentPrimalityMatrix[0] = false;
            segmentPrimalityMatrix[1] = false;
            segmentPrimalityMatrix[2] = true;

            int primeCandidate;
            for (primeCandidate = currentLowerBound; primeCandidate < Math.Sqrt(currentUpperBound); primeCandidate++)
            {
                if (!segmentPrimalityMatrix[primeCandidate]) continue;  // skip non-prime numbers

                // strike out any non-prime multiples of current candidate
                for (int i = primeCandidate * primeCandidate; i < currentUpperBound; i += 2 * primeCandidate)
                {
                    segmentPrimalityMatrix[i] = false;
                }
            }

            for (int n = 0; n < segmentPrimalityMatrix.Length; n++)
            {
                if (segmentPrimalityMatrix[n])
                {
                    primes.Add(n);
                }

                segmentPrimalityMatrix[n] = true;
            }

            /* Sieving intermediate segments */

            int p, currentPrime;
            double primeLimit;
            for (int i = 1; i < segmentCount - 1; i++)
            {
                // increment bounds to next segment
                currentLowerBound = currentUpperBound;
                currentUpperBound += segmentSize;

                // for each currently found prime less than the square root of the current upper segment bound
                p = 0;
                currentPrime = primes[p];
                primeLimit = Math.Sqrt(currentUpperBound);
                while (currentPrime < primeLimit)
                {
                    // find the lowest multiple of the current prime in the current segment
                    int delta = currentLowerBound % currentPrime;
                    int lowestPrimeMultiple = currentLowerBound + (delta != 0 ? (currentPrime - delta) : 0);

                    for (int j = lowestPrimeMultiple; j < currentUpperBound; j += currentPrime)
                    {
                        segmentPrimalityMatrix[j - currentLowerBound] = false;
                    }

                    currentPrime = primes[++p];
                }

                // adding found primes to list and resetting segment primality matrix
                for (int n = 0; n + currentLowerBound < currentUpperBound; n++)
                {
                    if (segmentPrimalityMatrix[n])
                    {
                        primes.Add(n + currentLowerBound);
                    }

                    segmentPrimalityMatrix[n] = true;
                }
            }

            /* Sieve tail segment */

            // increment bounds to next segment
            currentLowerBound = currentUpperBound;
            currentUpperBound = upperBound;

            // for each currently found prime less than the square root of the current upper segment bound
            p = 0;
            currentPrime = primes[p];
            primeLimit = Math.Sqrt(currentUpperBound);
            while (currentPrime < primeLimit)
            {
                // find the lowest multiple of the current prime in the current segment
                int delta = currentLowerBound % currentPrime;
                int lowestPrimeMultiple = currentLowerBound + (delta != 0 ? (currentPrime - delta) : 0);

                for (int j = lowestPrimeMultiple; j < currentUpperBound; j += currentPrime)
                {
                    segmentPrimalityMatrix[j - currentLowerBound] = false;
                }

                currentPrime = primes[++p];
            }

            // adding found primes to list and resetting segment primality matrix
            for (int n = 0; n + currentLowerBound < currentUpperBound; n++)
            {
                if (segmentPrimalityMatrix[n])
                {
                    primes.Add(n + currentLowerBound);
                }

                segmentPrimalityMatrix[n] = true;
            }

            return primes;
        }
    }
}