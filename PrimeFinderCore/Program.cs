using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PrimeFinderCore
{
    /// <summary>
    /// Represent a set of bounds that should be checked for prime numbers.
    /// </summary>
    internal readonly struct PrimeBounds
    {
        internal readonly ulong LowerBound;

        internal readonly ulong UpperBound;

        /// <summary>
        /// Initialises a new instance of the <see cref="PrimeBounds"/> struct.
        /// </summary>
        internal PrimeBounds(ulong lowerBound, ulong upperBound)
        {
            LowerBound = lowerBound;

            UpperBound = upperBound;
        }
    }

    /// <summary>
    /// Represents a batch of numbers that should be searched for primes.
    /// </summary>
    internal struct PrimeFinderBatch : IBatch<PrimeBounds, ulong[]>
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="PrimeFinderBatch"/> struct.
        /// </summary>
        /// <param name="bounds">The bounds that should be searched for primes.</param>
        public PrimeFinderBatch(PrimeBounds bounds)
        {
            IsCompleted = false;

            Input = bounds;

            Output = new ulong[0];
        }

        /// <inheritdoc />
        public PrimeBounds Input { get; }

        /// <inheritdoc />
        public bool IsCompleted { get; set; }

        /// <inheritdoc />
        public ulong[] Output { get; set; }

        /// <summary>
        /// Checks whether the given number is prime.
        /// </summary>
        /// <param name="number">The number to check.</param>
        /// <returns>Whether the given number is prime or not.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckPrime(ulong number)
        {
            if (number <= 1)
            {
                return false;
            }

            if (number <= 3)
            {
                return true;
            }

            if (number % 2 == 0 || number % 3 == 0)
            {
                return false;
            }

            uint i = 5;
            while (i * i <= number)
            {
                if (number % i == 0 || number % (i + 2) == 0)
                {
                    return false;
                }

                i += 6;
            }
            return true;
        }

        /// <inheritdoc />
        public void Process()
        {
            List<ulong> filteredPrimes = new List<ulong>();

            for (ulong i = Input.LowerBound; i < Input.UpperBound; i++)
            {
                if (CheckPrime(i))
                {
                    filteredPrimes.Add(i);
                }
            }

            Output = filteredPrimes.ToArray();
            IsCompleted = true;
        }
    }

    internal class Program
    {
        /// <summary>
        /// The number of threads that should batch out work to the worker threads.
        /// </summary>
        private static readonly int BatcherThreadCount = 1;

        /// <summary>
        /// The number of threads that should collate completed batches from the worker threads.
        /// </summary>
        private static readonly int MergerThreadCount = 1;

        /// <summary>
        /// The number of threads that should process batches.
        /// </summary>
        private static readonly int WorkerThreadCount = 1;

        /// <summary>
        /// Allows for work to be done in parallel.
        /// </summary>
        private static ThreadedBatcher<PrimeBounds, ulong[]> batcher;

        /// <summary>
        /// Initialises a new instance of the <see cref="Program"/> class.
        /// </summary>
        static Program()
        {
            batcher = new ThreadedBatcher<PrimeBounds, ulong[]>(BatcherThreadCount, WorkerThreadCount, MergerThreadCount);
        }

        /// <summary>
        /// Collates processed batches into the final <see cref="SinkState"/> instance.
        /// </summary>
        private static void CollateBatch(IBatch<PrimeBounds, ulong[]> processedBatch, SinkState state)
        {
#if DEBUG
            Console.WriteLine($"Sink thread received {processedBatch.Output.Length} primes to merge into output list.");
#endif
            lock (state.FoundPrimes)
            {
                state.FoundPrimes.AddRange(processedBatch.Output);
            }
        }

        /// <summary>
        /// Generates <see cref="PrimeFinderBatch"/> instances to be computed.
        /// </summary>
        private static IBatch<PrimeBounds, ulong[]> GenerateBatch(int sourceThreadId, SourceState state)
        {
            if (state.CurrentLowerBound >= state.HighestBound)
            {
                return new ThreadedBatcher<PrimeBounds, ulong[]>.SentinelBatch();
            }
            else
            {
                // create a new prime bound, increment lower and upper bounds, and return a new batch
                PrimeBounds newBounds = new PrimeBounds(state.CurrentLowerBound,
                    state.CurrentUpperBound > state.HighestBound ? state.HighestBound : state.CurrentUpperBound);

                state.CurrentLowerBound = state.CurrentUpperBound + 1;
                state.CurrentUpperBound += SourceState.Increment;

                // cap upper bound to the highest possible bound
                state.CurrentUpperBound = state.CurrentUpperBound > state.HighestBound
                    ? state.HighestBound
                    : state.CurrentUpperBound;

                return new PrimeFinderBatch(newBounds);
            }
        }

        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // we created explicit types to utilise generic type-safety guarantees
            SourceState sourceState = new SourceState
            {
                LowestBound = 2,
                HighestBound = 100_000_000,
                CurrentLowerBound = 0,
                CurrentUpperBound = 0 + SourceState.Increment
            };

            WorkerState workerState = new WorkerState();

            SinkState sinkState = new SinkState();

            int workerThreadCount;
            do
            {
                try
                {
                    Console.WriteLine("How many threads should be ran in parallel:");
                    string inp = Console.ReadLine();
                    workerThreadCount = Convert.ToInt32(inp);
                    workerThreadCount = workerThreadCount < 0 ? 1 : workerThreadCount;
                    break;
                }
                catch (FormatException)
                {
                    Console.WriteLine("Invalid format. Please try again.");
                }
            } while (true);

            batcher = new ThreadedBatcher<PrimeBounds, ulong[]>(BatcherThreadCount, workerThreadCount, MergerThreadCount);

            Console.WriteLine("Initialising batcher.");
            batcher.Initialise(GenerateBatch, sourceState, ProcessBatch, workerState, CollateBatch, sinkState);

            long elapsedMilliseconds = batcher.UntilFinished();

            Console.WriteLine("Batcher finished.");
            Console.WriteLine(
                $"Found {sinkState.FoundPrimes.Count} primes between {sourceState.LowestBound} and {sourceState.HighestBound}" +
                $" in {elapsedMilliseconds} milliseconds.");

            Console.WriteLine("Goodbye World!");
        }

        /// <summary>
        /// Processes <see cref="PrimeFinderBatch"/> instances using the Sieve of Eratosthenes.
        /// </summary>
        private static void ProcessBatch(IBatch<PrimeBounds, ulong[]> newBatch, WorkerState state)
        {
            newBatch.Process();
        }
    }

    /// <summary>
    /// Sink thread state object. Used to merge batches into one final output list.
    /// </summary>
    internal class SinkState
    {
        internal List<ulong> FoundPrimes { get; set; } = new List<ulong>();
    }

    /// <summary>
    /// Source thread state object. Used to create batches.
    /// </summary>
    internal class SourceState
    {
        internal const ulong Increment = 100_000;

        internal ulong CurrentLowerBound { get; set; }
        internal ulong CurrentUpperBound { get; set; }
        internal ulong HighestBound { get; set; }
        internal ulong LowestBound { get; set; }
    }

    /// <summary>
    /// Worker thread state object. Empty for now.
    /// </summary>
    internal class WorkerState
    {
    }
}