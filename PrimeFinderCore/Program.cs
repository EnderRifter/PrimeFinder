using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PrimeFinderCore
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            const int upperPrimeBound = 15485864; //int.MaxValue; //1_000_000_000;
            const int segmentSize = 3_000; //40_000; //10_000;

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

            //SieveDiff(SieveOfEratosthenesOptimised, bound => SegmentedSieveOfEratosthenes(bound, segmentSize), upperPrimeBound);

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

            for (int prime = 2; prime < upperBound; prime++)
            {
                if (!primalityMatrix[prime - 1]) continue;  // ignore non-prime number

                primes.Add(prime);

                // sieve out all multiples of current candidate
                for (int primeMultiple = 2 * prime; primeMultiple > 0 && primeMultiple < upperBound; primeMultiple += prime)
                {
                    primalityMatrix[primeMultiple - 1] = false;
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

            List<int> primes = new List<int> { 2 };

            double maximumTestedPrime = Math.Sqrt(upperBound);
            int prime;
            for (prime = 3; prime < maximumTestedPrime; prime += 2)
            {
                if (!primalityMatrix[(prime / 2) - 1]) continue;  // skip non-prime numbers

                primes.Add(prime);

                // strike out any non-prime multiples of current candidate
                for (int primeMultiple = prime * prime; primeMultiple > 0 && primeMultiple < upperBound; primeMultiple += 2 * prime)
                {
                    primalityMatrix[(primeMultiple / 2) - 1] = false;
                }
            }

            // read remains of buffer to find the primes left and add them to the list
            for (int number = prime; number < upperBound; number += 2)
            {
                if (primalityMatrix[(number / 2) - 1])
                {
                    primes.Add(number);
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
            
            static void SieveRootSegment(int upperBound, ref bool[] segmentPrimalityMatrix, ref List<int> primeSet)
            {
                double maximumTestedPrime = Math.Sqrt(upperBound);
                int prime;
                for (prime = 3; prime < maximumTestedPrime; prime += 2)
                {
                    if (!segmentPrimalityMatrix[(prime / 2) - 1]) continue;  // skip non-prime numbers

                    primeSet.Add(prime);

                    // strike out any non-prime multiples of current candidate
                    for (int primeMultiple = prime * prime; primeMultiple > 0 && primeMultiple < upperBound; primeMultiple += 2 * prime)
                    {
                        segmentPrimalityMatrix[(primeMultiple / 2) - 1] = false;
                    }
                }

                for (int number = prime; number < upperBound; number += 2)
                {
                    if (segmentPrimalityMatrix[(number / 2) - 1])
                    {
                        primeSet.Add(number);
                    }
                }
            }

            static void SieveSegment(int lowerBound, int upperBound, ref bool[] segmentPrimalityMatrix,
                ref List<int> primeSet)
            {
                double maximumTestedPrime = Math.Sqrt(upperBound);

                //Console.WriteLine($"Sieving segment: {lowerBound} -> {upperBound} (size: {upperBound - lowerBound})");

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

            List<int> primes = new List<int> { 2 };

            int segmentLowerBound = 3;
            int newSegmentUpperBound = segmentLowerBound + segmentSize;
            int segmentUpperBound = newSegmentUpperBound > upperBound || newSegmentUpperBound < 0
                ? upperBound
                : newSegmentUpperBound;

            /* Sieving root segment for initial primes */

            bool[] rootSegmentPrimalityMatrix = new bool[(segmentUpperBound - segmentLowerBound) / 2];

            for (int i = 0; i < rootSegmentPrimalityMatrix.Length; i++)
            {
                rootSegmentPrimalityMatrix[i] = true;
            }

            SieveRootSegment(segmentUpperBound, ref rootSegmentPrimalityMatrix, ref primes);

            /* Sieving remaining segments */

            bool[] segmentPrimalityMatrix = new bool[segmentSize];

            for (int i = 0; i < segmentPrimalityMatrix.Length; i++)
            {
                segmentPrimalityMatrix[i] = true;
            }

            do
            {
                segmentLowerBound = segmentUpperBound;
                newSegmentUpperBound = segmentLowerBound + segmentSize;

                if (newSegmentUpperBound > upperBound || newSegmentUpperBound < 0)
                {
                    // our exit condition. we have reached the end and either exceeded the upper bound, or wrapped around
                    segmentUpperBound = upperBound;

                    int currentSegmentSize = upperBound - segmentLowerBound;

                    //Console.WriteLine($"Sieving final segment: {segmentLowerBound} -> {segmentUpperBound} (size: {currentSegmentSize})");

                    if (segmentPrimalityMatrix.Length > currentSegmentSize)
                    {
                        // we need to blot out any values that are greater than our upper bound.
                        // this occurs when the buffer is larger than the current search space
                        for (int i = currentSegmentSize; i < segmentPrimalityMatrix.Length; i++)
                        {
                            segmentPrimalityMatrix[i] = false;
                        }
                    }
                }
                else
                {
                    // we continue as normal
                    segmentUpperBound = newSegmentUpperBound;
                }

                SieveSegment(segmentLowerBound, segmentUpperBound, ref segmentPrimalityMatrix, ref primes);

            } while (segmentUpperBound != upperBound);

            return primes;
        }
    }
}