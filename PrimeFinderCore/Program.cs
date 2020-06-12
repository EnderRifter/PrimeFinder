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

            const ulong upperPrimeBound = uint.MaxValue;
            const ulong segmentSize = 44_000;

            static void Benchmark(Func<ulong, List<ulong>> sieveFunc, ulong upperPrimeBound)
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();
                List<ulong> primes = sieveFunc(upperPrimeBound);
                stopwatch.Stop();

                Console.WriteLine($"We found {primes.Count} primes below {upperPrimeBound} with {sieveFunc.Method.Name} in {stopwatch.ElapsedTicks} ticks ({stopwatch.ElapsedMilliseconds} ms)");

                GC.Collect();
            }

            static void BenchmarkSegmented(Func<ulong, ulong, List<ulong>> sieveFunc, ulong upperPrimeBound, ulong segmentSize)
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();
                List<ulong> primes = sieveFunc(upperPrimeBound, segmentSize);
                stopwatch.Stop();

                Console.WriteLine($"We found {primes.Count} primes below {upperPrimeBound} with {sieveFunc.Method.Name} in {stopwatch.ElapsedTicks} ticks ({stopwatch.ElapsedMilliseconds} ms)");

                GC.Collect();
            }

            static void SieveDiff(Func<ulong, List<ulong>> sieveA, Func<ulong, List<ulong>> sieveB, ulong upperPrimeBound)
            {
                List<ulong> primeListA = sieveA(upperPrimeBound);
                HashSet<ulong> primeSetA = new HashSet<ulong>(primeListA);
                GC.Collect();

                List<ulong> primeListB = sieveB(upperPrimeBound);
                HashSet<ulong> primeSetB = new HashSet<ulong>(primeListB);
                GC.Collect();

                Console.WriteLine($"Found {primeListA.Count} primes with sieve A, and {primeListB.Count} primes with sieve B");

                List<ulong> diff = new List<ulong>();

                foreach (ulong item in primeListA)
                {
                    if (primeSetB.Contains(item)) continue;

                    Console.WriteLine($"Primes list A has member {item}, which does not appear in prime list B.");
                    diff.Add(item);
                }

                foreach (ulong item in primeListB)
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

            //Benchmark(SieveOfEratosthenesOptimised, upperPrimeBound);

            //Benchmark(SieveOfSundaram, upperPrimeBound);

            BenchmarkSegmented(SegmentedSieveOfEratosthenes, upperPrimeBound, segmentSize);

            Console.WriteLine("Goodbye World!");
        }

        private static List<ulong> SieveOfEratosthenes(ulong upperBound)
        {
            if (upperBound < 2)
            {
                throw new ArgumentException("No primes in desired range; upperBound too small!");
            }
            
            if (upperBound == 2)
            {
                return new List<ulong> { 2 };
            }

            List<ulong> primes = new List<ulong>();

            bool[] nonPrimalityMatrix = new bool[upperBound - 2];

            for (ulong prime = 2; prime < upperBound; prime++)
            {
                if (nonPrimalityMatrix[prime - 2]) continue;  // ignore non-prime number

                primes.Add(prime);

                // sieve out all multiples of current candidate
                for (ulong primeMultiple = 2 * prime; primeMultiple < upperBound; primeMultiple += prime)
                {
                    nonPrimalityMatrix[primeMultiple - 2] = true;
                }
            }

            return primes;
        }

        private static List<ulong> SieveOfEratosthenesOptimised(ulong upperBound)
        {
            if (upperBound < 2)
            {
                throw new ArgumentException("No primes in desired range; upperBound too small!");
            }

            if (upperBound == 2)
            {
                return new List<ulong> { 2 };
            }

            bool[] nonPrimalityMatrix = new bool[upperBound / 2];

            List<ulong> primes = new List<ulong> { 2 };

            double maximumTestedPrime = Math.Sqrt(upperBound);
            ulong prime;
            for (prime = 3; prime < maximumTestedPrime; prime += 2)
            {
                if (nonPrimalityMatrix[(prime / 2) - 1]) continue;  // skip non-prime numbers

                primes.Add(prime);

                // strike out any non-prime multiples of current candidate
                for (ulong primeMultiple = prime * prime; primeMultiple < upperBound; primeMultiple += 2 * prime)
                {
                    nonPrimalityMatrix[(primeMultiple / 2) - 1] = true;
                }
            }

            // read remains of buffer to find the primes left and add them to the list
            for (ulong number = prime; number < upperBound; number += 2)
            {
                if (!nonPrimalityMatrix[(number / 2) - 1])
                {
                    primes.Add(number);
                }
            }

            return primes;
        }

        private static List<ulong> SegmentedSieveOfEratosthenes(ulong upperBound, ulong segmentSize)
        {
            if (upperBound < 2)
            {
                throw new ArgumentException("No primes in desired range; upperBound too small!");
            }

            if (upperBound == 2)
            {
                return new List<ulong> { 2 };
            }

            if (segmentSize > Math.Sqrt(upperBound))
            {
                throw new ArgumentException("The segment size cannot be greater than sqrt(upperBound)!");
            }
            
            static void SieveRootSegment(ulong upperBound, ref bool[] segmentNonPrimalityMatrix, ref List<ulong> primeSet)
            {
                double maximumTestedPrime = Math.Sqrt(upperBound);
                ulong prime;
                for (prime = 3; prime < maximumTestedPrime; prime += 2)
                {
                    if (segmentNonPrimalityMatrix[(prime / 2) - 1]) continue;  // skip non-prime numbers

                    primeSet.Add(prime);

                    // strike out any non-prime multiples of current candidate
                    for (ulong primeMultiple = prime * prime; primeMultiple < upperBound; primeMultiple += 2 * prime)
                    {
                        segmentNonPrimalityMatrix[(primeMultiple / 2) - 1] = true;
                    }
                }

                for (ulong number = prime; number < upperBound; number += 2)
                {
                    if (!segmentNonPrimalityMatrix[(number / 2) - 1])
                    {
                        primeSet.Add(number);
                    }
                }
            }

            static void SieveSegment(ulong lowerBound, ulong upperBound, ref bool[] segmentNonPrimalityMatrix,
                ref List<ulong> primeSet)
            {
                double maximumTestedPrime = Math.Sqrt(upperBound);

                for (int p = 0; p < primeSet.Count; p++)
                {
                    ulong prime = primeSet[p];

                    if (prime > maximumTestedPrime)
                    {
                        break;
                    }

                    // find the lowest multiple of the current prime in the current segment's range
                    ulong delta = lowerBound % prime;
                    ulong lowestPrimeMultiple = lowerBound + (delta != 0 ? (prime - delta) : 0);

                    // strike out all candidates in range starting from said multiple
                    for (ulong primeMultiple = lowestPrimeMultiple; primeMultiple < upperBound; primeMultiple += prime)
                    {
                        ulong packedMultiple = primeMultiple - lowerBound;

                        segmentNonPrimalityMatrix[packedMultiple] = true;
                    }
                }

                for (int i = 0; i < segmentNonPrimalityMatrix.Length; i++)
                {
                    if (!segmentNonPrimalityMatrix[i])
                    {
                        ulong unpackedPrime = (ulong)i + lowerBound;

                        primeSet.Add(unpackedPrime);
                    }

                    segmentNonPrimalityMatrix[i] = false;
                }
            }

            List<ulong> primes = new List<ulong> { 2 };

            ulong segmentLowerBound = 3;
            ulong newSegmentUpperBound = segmentLowerBound + segmentSize;
            ulong segmentUpperBound = newSegmentUpperBound > upperBound
                ? upperBound
                : newSegmentUpperBound;

            /* Sieving root segment for initial primes */

            bool[] rootSegmentNonPrimalityMatrix = new bool[(segmentUpperBound - segmentLowerBound) / 2];

            SieveRootSegment(segmentUpperBound, ref rootSegmentNonPrimalityMatrix, ref primes);

            /* Sieving remaining segments */

            bool[] segmentNonPrimalityMatrix = new bool[segmentSize];

            do
            {
                segmentLowerBound = segmentUpperBound;
                newSegmentUpperBound = segmentLowerBound + segmentSize;

                if (newSegmentUpperBound > upperBound)
                {
                    // our exit condition. we have reached the end and either exceeded the upper bound, or wrapped around
                    segmentUpperBound = upperBound;

                    ulong currentSegmentSize = upperBound - segmentLowerBound;

                    //Console.WriteLine($"Sieving final segment: {segmentLowerBound} -> {segmentUpperBound} (size: {currentSegmentSize})");

                    if ((ulong)segmentNonPrimalityMatrix.Length > currentSegmentSize)
                    {
                        // we need to blot out any values that are greater than our upper bound.
                        // this occurs when the buffer is larger than the current search space
                        for (ulong i = currentSegmentSize; i < (ulong)segmentNonPrimalityMatrix.Length; i++)
                        {
                            segmentNonPrimalityMatrix[i] = true;
                        }
                    }
                }
                else
                {
                    // we continue as normal
                    segmentUpperBound = newSegmentUpperBound;
                }

                SieveSegment(segmentLowerBound, segmentUpperBound, ref segmentNonPrimalityMatrix, ref primes);

            } while (segmentUpperBound != upperBound);

            return primes;
        }

        private static List<ulong> SieveOfSundaram(ulong upperBound)
        {
            if (upperBound < 2)
            {
                throw new ArgumentException("No primes in desired range; upperBound too small!");
            }

            if (upperBound == 2)
            {
                return new List<ulong> { 2 };
            }

            List<ulong> primes = new List<ulong> { 2 };

            ulong modifiedUpperBound = (upperBound - 2) / 2;

            bool[] nonPrimalityMatrix = new bool[modifiedUpperBound + 1];

            ulong i = 1;
            while (i < modifiedUpperBound + 1)
            {
                ulong j = i;

                while ((i + j + 2 * i * j) <= modifiedUpperBound)
                {
                    nonPrimalityMatrix[(i + j + 2 * i * j)] = true;

                    j++;
                }

                i++;
            }

            ulong number = 1;
            while (number < modifiedUpperBound + 1)
            {
                if (!nonPrimalityMatrix[number])
                {
                    primes.Add((2 * number) + 1);
                }

                number++;
            }

            return primes;
        }
    }
}