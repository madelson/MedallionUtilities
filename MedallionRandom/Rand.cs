using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Random
{
    using System.Security.Cryptography;
    using System.Threading;
    using Random = System.Random;

    // java methods
    // streams
    // global methods
    // security RandomNumberGenerator back-and-forth

    public static class Rand
    {
        #region ---- Utility Extensions ----
        public static bool NextBoolean(this Random random)
        {
            Throw.IfNull(random, nameof(random));

            return (random.Next() & 1) == 1;
        }

        public static bool NextBoolean(this Random random, double probability)
        {
            Throw.IfNull(random, nameof(random));
            Throw.IfOutOfRange(probability, nameof(probability), min: 0, max: 1);

            if (probability == 0)
            {
                return false;
            }
            if (probability == 1)
            {
                return true;
            }

            return random.NextDouble() < probability;
        }

        public static int NextInt32(this Random random)
        {
            Throw.IfNull(random, nameof(random));

            return ((random.Next() & ushort.MaxValue) << 16) + (random.Next() & ushort.MaxValue);
        }

        public static long NextInt64(this Random random)
        {
            Throw.IfNull(random, nameof(random));

            const long Mask24 = 0xFFFFFF;

            return ((random.Next() & Mask24) << 48)
                + ((random.Next() & Mask24) << 24)
                + (random.Next() & ushort.MaxValue);
        }

        #region ---- Streams ----
        public static IEnumerable<double> NextDoubles(this Random random)
        {
            Throw.IfNull(random, nameof(random));

            return NextDoublesIterator(random);
        }

        private static IEnumerable<double> NextDoublesIterator(Random random)
        {
            while (true)
            {
                yield return random.NextDouble();
            }
        }

        public static IEnumerable<byte> NextBytes(this Random random)
        {
            Throw.IfNull(random, nameof(random));

            return NextBytesIterator(random);
        }

        private static IEnumerable<byte> NextBytesIterator(Random random)
        {
            var buffer = new byte[256];
            while (true)
            {
                random.NextBytes(buffer);
                for (var i = 0; i < buffer.Length; ++i)
                {
                    yield return buffer[i];
                }
            }
        }
        #endregion

        #region ---- Shuffling ----
        public static IEnumerable<T> Shuffled<T>(this IEnumerable<T> source, Random random = null)
        {
            Throw.IfNull(source, nameof(source));
            
            return ShuffledIterator(source, random);
        }

        private static IEnumerable<T> ShuffledIterator<T>(IEnumerable<T> source, Random random)
        {
            var list = source.ToList();
            if (list.Count == 0)
            {
                yield break;
            }

            Random rand;
            if (random == null)
            {
                // when we aren't provided with a Random, we generally have to 
                // fall back on Create() instead of ThreadLocalRandom here because we have no way
                // of knowing whether the iterator will be processed only on a single thread.
                // However, for small lists we can avoid creating a new random by doing a non-lazy shuffle

                if (list.Count <= 20)
                {
                    list.Shuffle(random);
                    foreach (var item in list)
                    {
                        yield return item;
                    }
                    yield break;
                }

                rand = Create();
            }
            else
            {
                rand = random;
            }
            
            for (var i = 0; i < list.Count - 1; ++i)
            {
                // swap i with a random index and yield the swapped value
                var randomIndex = rand.Next(minValue: i, maxValue: list.Count);
                var randomValue = list[randomIndex];
                list[randomIndex] = list[i];
                // note that we don't even have to put randomValue in list[i], because this is a throwaway list!
                yield return randomValue;
            }

            // yield the last value
            yield return list[list.Count - 1];
        }

        public static void Shuffle<T>(this IList<T> list, Random random = null)
        {
            Throw.IfNull(list, nameof(list));
            var rand = random ?? ThreadLocalRandom;

            for (var i = 0; i < list.Count - 1; ++i)
            {
                // swap i with a random index
                var randomIndex = rand.Next(minValue: i, maxValue: list.Count);
                var randomValue = list[randomIndex];
                list[randomIndex] = list[i];
                list[i] = randomValue;
            }
        }
        #endregion
        #endregion

        #region ---- ThreadLocal ----
        // ideas for GetCurrent()
        // 1. like an object pool: gives you the current one wrapped in a disposable to put it back
        // 2. a wrapped instance that asserts Thread.CurrentThread == owningThread on each call.

        [ThreadStatic]
        private static Random threadLocalRandom;

        private static Random ThreadLocalRandom { get { return threadLocalRandom ?? (threadLocalRandom = Create()); } }

        public static double NextDouble()
        {
            return ThreadLocalRandom.NextDouble();
        }

        public static int Next()
        {
            return ThreadLocalRandom.Next();
        }

        public static int Next(int minValue, int maxValue)
        {
            return ThreadLocalRandom.Next(minValue, maxValue);
        }

        public static void NextBytes(byte[] buffer)
        {
            ThreadLocalRandom.NextBytes(buffer);
        }

        // TODO do we want these?
        public static bool NextBoolean()
        {
            return ThreadLocalRandom.NextBoolean();
        }

        public static bool NextBoolean(double probability)
        {
            return ThreadLocalRandom.NextBoolean(probability);
        }
        #endregion

        #region ---- Factories ----
        private static int nextSeed;

        public static Random Create()
        {
            var combinedSeed = unchecked((31 * Interlocked.Increment(ref nextSeed)) + Environment.TickCount);
            return new Random(combinedSeed);
        }
        #endregion

        #region ---- RandomNumberGenerator Interop ----
        public static Random AsRandom(this RandomNumberGenerator randomNumberGenerator)
        {
            Throw.IfNull(randomNumberGenerator, nameof(randomNumberGenerator));

            return new RandomNumberGeneratorRandom(randomNumberGenerator);
        }

        private sealed class RandomNumberGeneratorRandom : Random
        {
            private const int BufferLength = 512;

            private readonly RandomNumberGenerator rand;
            private readonly byte[] buffer = new byte[BufferLength];
            private int nextByteIndex;

            public RandomNumberGeneratorRandom(RandomNumberGenerator randomNumberGenerator)
                : base(Seed: 0) // avoid having to generate a time-based seed 
            {
                this.rand = randomNumberGenerator;
            }

            private int BufferSize { get { return BufferLength - this.nextByteIndex; } }

            // From MSDN https://msdn.microsoft.com/en-us/library/system.random%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
            // You can implement your own random number generator by inheriting from the Random class and supplying 
            // your random number generation algorithm.To supply your own algorithm, you must override the Sample method, 
            // which implements the random number generation algorithm.You should also override the Next(), Next(Int32, Int32), 
            // and NextBytes methods to ensure that they call your overridden Sample method.You don't have to override the 
            // Next(Int32) and NextDouble methods

            protected override double Sample()
            {
                // based on https://docs.oracle.com/javase/8/docs/api/java/util/Random.html#nextDouble--
                return (((long)this.NextBits(26) << 27) + this.NextBits(27)) / (double)(1L << 53);
            }

            public override int Next()
            {
                return checked((int)this.InternalNext(maxValue: int.MaxValue));
            }

            public override int Next(int minValue, int maxValue)
            {
                Throw.If(minValue > maxValue, nameof(minValue), nameof(minValue) + " must be < " + nameof(maxValue));

                // this optimization for powers of two from https://docs.oracle.com/javase/8/docs/api/java/util/Random.html#nextInt-int-
                // We leave this outside of InternalNext() because Next() won't ever benefit from it
                var difference = (long)maxValue - minValue;
                if (difference <= int.MaxValue && (difference & -difference) == difference)
                {
                    return minValue + (int)((difference * this.NextBits(31)) >> 31);
                }

                return checked((int)(minValue + this.InternalNext(maxValue: difference)));
            }

            private long InternalNext(long maxValue)
            {
                // based on .NET random's implementation of Next(int, int). I'm assuming that our Sample()
                // implementation is good enough to work with large ranges (given that it divides by 2^53 and
                // the biggest range we'll ever get here is 2*int.MaxValue + 1, or ~2^32)
                return (long)(maxValue * this.Sample());
            }

            // based on the java Random API: bits must be in [0..32]
            private int NextBits(int bits)
            {
                if ((this.BufferSize * 8) < bits)
                {
                    this.rand.GetBytes(buffer);
                    this.nextByteIndex = 0;
                }
                
                var result = 0;
                int bitsLoaded;
                for (bitsLoaded = 0; bitsLoaded < bits; bitsLoaded += 8)
                {
                    result += this.buffer[this.nextByteIndex++] << bitsLoaded;
                }

                var remainderBits = bits & 7; // equivalent to bits % 8
                if (remainderBits > 0)
                {
                    result += (this.buffer[this.nextByteIndex++] >> (8 - remainderBits)) << bitsLoaded;
                }

                return result;
            }

            // we override this for performance reasons, since we can call the underlying RNG's NextBytes() method directly
            public override void NextBytes(byte[] buffer)
            {
                if (buffer.Length <= this.BufferSize)
                {
                    for (var i = this.nextByteIndex; i < buffer.Length; ++i)
                    {
                        buffer[i] = this.buffer[i];
                    }
                    this.nextByteIndex += buffer.Length;
                }
                else
                {
                    this.rand.GetBytes(buffer);
                }
            }
        }
        #endregion
    }
}
