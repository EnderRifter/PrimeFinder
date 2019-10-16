using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrimeNumberFinder
{
    internal class MultithreadedPrimeFinder
    {
        public List<uint> Primes { get; set; } = new List<uint>() { };
        private byte liveBatchCount = 0;
        private byte maxBatches = 8;
        private uint batchSize = 1_000_000;
        private uint upperBound = 10_000_000;
        private uint searchPointsLeft = 10_000_000;
        private uint currentMin = 0;
        private uint currentMax = 1_000_000;

        private List<BatchPrimeFinder> primeFinders;

        private bool quit = false;
        private PrintTypes printType;

        private PrimeWriter writer = new PrimeWriter(@".\primes.txt");

        public MultithreadedPrimeFinder()
        {
            searchPointsLeft = upperBound;
        }

        public MultithreadedPrimeFinder(PrintTypes printType, uint upperBound, uint batchSize = 1_000_000, byte maxBatches = 8) : this()
        {
            this.upperBound = upperBound;

            if (upperBound < batchSize)
            {
                batchSize = upperBound;
            }

            this.printType = printType;
            this.maxBatches = maxBatches;
        }

        public MultithreadedPrimeFinder(PrintTypes printType, bool max, uint batchSize = 1_000_000, byte maxBatches = 8) : this()
        {
            if (max)
            {
                upperBound = uint.MaxValue;
                searchPointsLeft = upperBound;
            }

            this.printType = printType;
            this.maxBatches = maxBatches;
        }

        public void Start()
        {
            Solve();
            writer.SavePrimes(Primes);
        }

        public void Stop()
        {
            quit = true;
            writer.SavePrimes(Primes);
        }

        private void Solve()
        {
            Console.WriteLine("Max Batches : {0}", maxBatches);
            primeFinders = new List<BatchPrimeFinder>(maxBatches);

            do
            {
                if (liveBatchCount < maxBatches)
                {
                    BatchPrimeFinder newBatch = CreateNewBatch(primeFinders.Count, false);
                    primeFinders.Add(newBatch);
                    newBatch.Start();
                }
            } while (searchPointsLeft >= batchSize && !quit);

            if (!quit)
            {
                if (searchPointsLeft > 0) //finish any remaining work
                {
                    BatchPrimeFinder newBatch = CreateNewBatch(primeFinders.Count, true);
                    primeFinders.Add(newBatch);
                    newBatch.Start();
                }
            }
        }

        private BatchPrimeFinder CreateNewBatch(int id, bool final)
        {
            BatchPrimeFinder batch;

            if (!final)
            {
                batch = new BatchPrimeFinder(currentMin, currentMax - 1, id);
            }
            else
            {
                batch = new BatchPrimeFinder(currentMin, currentMax, id);
            }

            batch.PrimesFound += HandlePrimesFound;

            if (!final)
            {
                currentMin = currentMax;
                if (currentMin + batchSize < upperBound)
                {
                    currentMax = currentMin + batchSize;
                }
                else
                {
                    currentMax = upperBound;
                }
            }

            liveBatchCount++;
            if (!final)
            {
                searchPointsLeft -= ((batch.UpperBound + 1) - batch.LowerBound);
            }
            else
            {
                searchPointsLeft = 0;
            }

            if (printType == PrintTypes.Normal)
            {
                Console.WriteLine("Batch min : {0}, Batch max : {1}, Search points left : {2}", batch.LowerBound, batch.UpperBound, searchPointsLeft);
            }
            else if (printType == PrintTypes.Fast)
            {
                Console.WriteLine("Search points left : {0}", searchPointsLeft);
            }
            else
            {
                Console.WriteLine("Searching for primes from {0} to {1}", batch.LowerBound, batch.UpperBound);
            }

            return batch;
        }

        private void HandlePrimesFound(BatchPrimeFinder batch)
        {
            Primes.AddRange(batch.Primes); //add calculated primes to list

            liveBatchCount--;

            if (printType == PrintTypes.Normal)
            {
                Console.WriteLine("Batch at thread '{0}' finished", batch.WorkerThread.Name);
            }

            batch.Stop(); //stop the batch, kills the thread
        }
    }
}