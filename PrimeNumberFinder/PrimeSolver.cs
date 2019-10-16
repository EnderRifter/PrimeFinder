using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrimeNumberFinder
{
    public class PrimeSolver
    {
        public List<uint> Primes { get; set; } = new List<uint> { 2, 3 };
        public bool Quit { get; set; } = false;

        public void Solve()
        {
            uint primeToCheck = Primes[Primes.Count - 1] + 2;

            while (!Quit)
            {
                if (CheckPrime(primeToCheck)) //number is prime
                {
                    Primes.Add(primeToCheck);

                    #region Printing Stats

                    if ((Primes.Count - 1) % 10_000 == 0)
                    {
                        OnTenThousandthPrime(primeToCheck, (uint)(Primes.Count - 1));
                    }

                    if ((Primes.Count - 1) % 100_000 == 0)
                    {
                        OnHundredThousandthPrime(primeToCheck, (uint)(Primes.Count - 1));
                    }

                    if ((Primes.Count - 1) % 1000000 == 0)
                    {
                        OnMillionthPrime(primeToCheck, (uint)(Primes.Count - 1));
                    }

                    #endregion Printing Stats
                }

                primeToCheck = primeToCheck + 2; //set next prime
            }
        }

        private bool CheckPrime(uint numberToCheck)
        {
            int i = 0;
            uint listLength = (uint)Math.Pow(Primes.Count, 1.0 / 2.0);
            bool isPrime = true;
            while (i < listLength && isPrime) //while the number is not disproven, and we haven't reached the next lowest prime
            {
                if (numberToCheck % Primes[i] == 0) //if number evenly divisible by current prime
                {
                    isPrime = false; //disproven as prime
                }
                i++;
            }

            return isPrime;
        }

        public List<uint> Stop()
        {
            Quit = true;
            return Primes;
        }

        #region Events

        public event Action<uint, uint> TenThousandthPrime;

        private void OnTenThousandthPrime(uint prime, uint primeCount)
        {
            TenThousandthPrime?.Invoke(prime, primeCount);
        }

        public event Action<uint, uint> HundredThousandthPrime;

        private void OnHundredThousandthPrime(uint prime, uint primeCount)
        {
            HundredThousandthPrime?.Invoke(prime, primeCount);
        }

        public event Action<uint, uint> MillionthPrime;

        private void OnMillionthPrime(uint prime, uint primeCount)
        {
            MillionthPrime?.Invoke(prime, primeCount);
        }

        #endregion Events
    }
}