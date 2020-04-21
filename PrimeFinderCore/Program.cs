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

            const int upperPrimeBound = 15485864; //1_000_000_000; //15485864;
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

            //Benchmark(SieveOfEratosthenes, upperPrimeBound);

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

            bool[] primalityMatrix = new bool[upperBound];

            for (int i = 2; i < primalityMatrix.Length; i++)
            {
                primalityMatrix[i - 1] = true;
            }

            primalityMatrix[0] = false;

            for (int primeCandidate = 2; primeCandidate < upperBound; primeCandidate++)
            {
                if (!primalityMatrix[primeCandidate - 1]) continue;  // ignore non-prime number

                primes.Add(primeCandidate);

                // sieve out all multiples of current candidate
                for (int i = 2 * primeCandidate; i < primalityMatrix.Length; i += primeCandidate)
                {
                    primalityMatrix[i - 1] = false;
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

            bool[] primalityMatrix = new bool[upperBound / 2];

            for (int i = 0; i < primalityMatrix.Length; i++)
            {
                primalityMatrix[i] = true;
            }

            List<int> primes = new List<int>() { 2 };

            double maximumTestedPrime = Math.Sqrt(upperBound);
            int primeCandidate;
            for (primeCandidate = 3; primeCandidate < maximumTestedPrime; primeCandidate += 2)
            {
                int mappedIndex = (primeCandidate / 2) - 1;

                if (!primalityMatrix[mappedIndex]) continue;  // skip non-prime numbers

                primes.Add(primeCandidate);

                // strike out any non-prime multiples of current candidate
                for (int i = primeCandidate * primeCandidate; i < upperBound; i += 2 * primeCandidate)
                {
                    primalityMatrix[(i / 2) - 1] = false;
                }
            }

            for (int n = primeCandidate; n < upperBound; n += 2)
            {
                if (primalityMatrix[(n / 2) - 1])
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

            List<int> primes = new List<int> { 2 };

            static void SieveRootSegment(int upperBound, ref bool[] segmentPrimalityMatrix, ref List<int> primeSet)
            {
                for (int i = 0; i < segmentPrimalityMatrix.Length; i++)
                {
                    segmentPrimalityMatrix[i] = true;
                }

                double maximumTestedPrime = Math.Sqrt(upperBound);
                int primeCandidate;
                for (primeCandidate = 3; primeCandidate < maximumTestedPrime; primeCandidate += 2)
                {
                    if (!segmentPrimalityMatrix[(primeCandidate / 2) - 1]) continue;  // skip non-prime numbers

                    primeSet.Add(primeCandidate);

                    // strike out any non-prime multiples of current candidate
                    for (int i = primeCandidate * primeCandidate; i < upperBound; i += 2 * primeCandidate)
                    {
                        segmentPrimalityMatrix[(i / 2) - 1] = false;
                    }
                }

                for (int n = primeCandidate; n < upperBound; n += 2)
                {
                    if (segmentPrimalityMatrix[(n / 2) - 1])
                    {
                        primeSet.Add(n);
                    }
                }
            }

            static void SieveSegment(int lowerBound, int upperBound, ref bool[] segmentPrimalityMatrix,
                ref List<int> primeSet)
            {
                double maximumTestedPrime = Math.Sqrt(upperBound);

                for (int p = 0; p < primeSet.Count; p++)
                {
                    int prime = primeSet[p];

                    if (prime > maximumTestedPrime)
                    {
                        break;
                    }
                    
                    // find the lowest multiple of the current prime in the current segment's range
                    int delta = lowerBound % prime;
                    int lowestPrimeMultiple = lowerBound + (delta != 0 ? (prime - delta) : 0);

                    // strike out all candidates in range starting from said multiple
                    for (int primeMultiple = lowestPrimeMultiple; primeMultiple > 0 && primeMultiple < upperBound; primeMultiple += prime)
                    {
                        int packedMultiple = primeMultiple - lowerBound;

                        segmentPrimalityMatrix[packedMultiple] = false;
                    }
                }

                for (int i = 0; i < segmentPrimalityMatrix.Length; i++)
                {
                    if (segmentPrimalityMatrix[i])
                    {
                        int unpackedPrime = i + lowerBound;

                        primeSet.Add(unpackedPrime);
                    }

                    segmentPrimalityMatrix[i] = true;
                }
            }

            int segmentLowerBound = 3;
            int segmentUpperBound = segmentLowerBound + segmentSize > upperBound
                ? upperBound
                : segmentLowerBound + segmentSize;

            bool[] rootSegmentPrimalityMatrix = new bool[(segmentUpperBound - segmentLowerBound) / 2];

            /* Sieving root segment for initial primes */

            SieveRootSegment(segmentUpperBound, ref rootSegmentPrimalityMatrix, ref primes);

            /* Sieving remaining segments */

            do
            {
                segmentLowerBound = segmentUpperBound;
                segmentUpperBound = segmentLowerBound + segmentSize > upperBound
                    ? upperBound
                    : segmentLowerBound + segmentSize;

                /* Resetting the segment primality matrix, allocating a new one if needed */
                int currentSegmentSize = segmentUpperBound - segmentLowerBound;

                bool[] segmentPrimalityMatrix = new bool[currentSegmentSize];

                for (int i = 0; i < segmentPrimalityMatrix.Length; i++)
                {
                    segmentPrimalityMatrix[i] = true;
                }

                SieveSegment(segmentLowerBound, segmentUpperBound, ref segmentPrimalityMatrix, ref primes);

            } while (segmentUpperBound != upperBound);

            return primes;
        }

        private static List<int> SegmentedSieveOfEratosthenesOld(int upperBound, int segmentSize)
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