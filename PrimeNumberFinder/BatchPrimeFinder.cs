using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrimeNumberFinder
{
    internal class BatchPrimeFinder
    {
        public uint LowerBound { get; private set; } = 0;
        public uint UpperBound { get; private set; } = 1_000_000;
        public List<uint> Primes { get; private set; } = new List<uint>();
        public Thread WorkerThread { get; private set; }
        public int ID { get; private set; }

        internal BatchPrimeFinder(uint lowerBound, uint upperBound, int id)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
            WorkerThread = new Thread(SolvePrimes);
            WorkerThread.Name = string.Format("Thread-{0}", id);
            ID = id;
        }

        public void Start()
        {
            WorkerThread.Start();
        }

        public void Stop()
        {
            WorkerThread.Abort();
        }

        private void SolvePrimes()
        {
            uint lower = LowerBound;
            uint upper = UpperBound;
            for (uint i = lower; i < upper; i++)
            {
                if (CheckPrime(i))
                {
                    Primes.Add(i);
                }
            }

            OnPrimesFound();
        }

        private bool CheckPrime(uint number)
        {
            if (number <= 1)
            {
                return false;
            }
            else if (number <= 3)
            {
                return true;
            }
            else if (number % 2 == 0 || number % 3 == 0)
            {
                return false;
            }

            uint i = 5;
            while ((i * i) <= number)
            {
                if (number % i == 0 || number % (i + 2) == 0)
                {
                    return false;
                }
                i += 6;
            }
            return true;
        }

        internal event Action<BatchPrimeFinder> PrimesFound;

        private void OnPrimesFound()
        {
            PrimesFound?.Invoke(this);
        }
    }
}