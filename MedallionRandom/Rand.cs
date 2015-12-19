using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Random
{
    using NullGuard;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using Random = System.Random;

    // java methods
    // nextgaussian

    [NullGuard(ValidationFlags.Arguments)]
    public static class Rand
    {
        #region ---- Java Extensions ----
        public static bool NextBoolean(this Random random)
        {
            return random.NextBits(1) != 0;
        }

        public static int NextInt32(this Random random)
        {
            return random.NextBits(32);
        }

        public static long NextInt64(this Random random)
        {
            var nextBitsRandom = random as NextBitsRandom;
            if (nextBitsRandom != null)
            {
                return ((long)nextBitsRandom.NextBits(32) << 32) + nextBitsRandom.NextBits(32);
            }

            // NextBits(32) for regular Random requires 2 calls to Next(), or 4 calls
            // total using the method above. Thus, we instead use an approach that requires
            // only 3 calls
            return ((long)random.Next30OrFewerBits(22) << 42)
                + ((long)random.Next30OrFewerBits(21) << 21)
                + random.Next30OrFewerBits(21);
        }

        public static float NextSingle(this Random random)
        {
            return random.NextBits(24) / ((float)(1 << 24));
        }

        public static IEnumerable<double> NextDoubles(this Random random)
        {
            return NextDoublesIterator(random);
        }

        private static IEnumerable<double> NextDoublesIterator(Random random)
        {
            while (true)
            {
                yield return random.NextDouble();
            }
        }

        private static int NextBits(this Random random, int bits)
        {
            var nextBitsRandom = random as NextBitsRandom;
            if (nextBitsRandom != null)
            {
                return nextBitsRandom.NextBits(bits);
            }

            // simulate with native random methods. 32 bits requires [int.MinValue, int.MaxValue]
            // and 31 bits requires [0, int.MaxValue]
            
            // 30 or fewer bits needs only one call
            if (bits <= 30)
            {
                return random.Next30OrFewerBits(bits);
            }
            
            var upperBits = random.Next30OrFewerBits(bits - 16) << 16;
            var lowerBits = random.Next30OrFewerBits(16);
            return upperBits + lowerBits;
        }

        private static int Next30OrFewerBits(this Random random, int bits)
        {
            // the simplest underlying call in Random is Next(), which gives us [0, int.MaxValue - 1).
            // That's not quite 31 bits, but if we discard the lowest bit, we get [0, 2^30), or 30 bits.

            // Note that we'll prefer to discard low bits throughout, since according to 
            // http://rosettacode.org/wiki/Subtractive_generator the low bits are less random

            return random.Next() >> (31 - bits);
        }
        #endregion

        #region ---- Other Extensions ----
        public static bool NextBoolean(this Random random, double probability)
        {
            if (probability == 0)
            {
                return false;
            }
            if (probability == 1)
            {
                return true;
            }

            if (probability < 0 || probability > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(probability), $"probability must be in [0, 1]. Found {probability}. ");
            }

            return random.NextDouble() < probability;
        }

        public static IEnumerable<byte> NextBytes(this Random random)
        {
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

        #region ---- Gaussian ----
        public static double NextGaussian(this Random random)
        {
            var nextGaussianRandom = random as INextGaussianRandom;
            if (nextGaussianRandom != null)
            {
                return nextGaussianRandom.NextGaussian();
            }

            double result, ignored;
            random.NextTwoGaussians(out result, out ignored);
            return result;
        }

        public static IEnumerable<double> NextGaussians(this Random random)
        {
            return NextGaussiansIterator(random);
        }

        private static IEnumerable<double> NextGaussiansIterator(Random random)
        {
            while (true)
            {
                double next, nextNext;
                random.NextTwoGaussians(out next, out nextNext);
                yield return next;
                yield return nextNext;
            }
        }

        private interface INextGaussianRandom
        {
            double NextGaussian();
        }

        private static void NextTwoGaussians(this Random random, out double value1, out double value2)
        {
            double v1, v2, s;
            do
            {
                v1 = 2 * random.NextDouble() - 1;   // between -1.0 and 1.0
                v2 = 2 * random.NextDouble() - 1;   // between -1.0 and 1.0
                s = v1 * v1 + v2 * v2;
            } while (s >= 1 || s == 0);
            double multiplier = Math.Sqrt(-2 * Math.Log(s) / s);

            value1 = v1* multiplier;
            value2 = v2 * multiplier; 
        }
        #endregion

        #region ---- ThreadLocal ----
        [ThreadStatic]
        private static SafeThreadLocalRandom threadLocalRandom;

        private static SafeThreadLocalRandom ThreadLocalRandom { get { return threadLocalRandom ?? (threadLocalRandom = new SafeThreadLocalRandom()); } }

        public static Random Current { get { return ThreadLocalRandom; } }

        public static double NextDouble()
        {
            return ThreadLocalRandom.UnsafeNextDouble();
        }
        
        public static int Next(int minValue, int maxValue)
        {
            return ThreadLocalRandom.UnsafeNext(minValue, maxValue);
        }
        
        // no NullGuard needed since this simply delegates
        private sealed class SafeThreadLocalRandom : Random, INextGaussianRandom
        {
            private readonly Thread thread;

            internal SafeThreadLocalRandom()
                : base(Seed: unchecked((31 * Thread.CurrentThread.ManagedThreadId) + Environment.TickCount))
            {
                this.thread = Thread.CurrentThread;
            }

            // note: we don't need to override Sample() because it's protected. This
            // allows us to have Unsafe() methods that call base, even though the base
            // methods might call Sample(). The disadvantage is that if later versions of
            // the framework add new methods which call Sample() under the hood, they
            // won't have thread-protection until we update this class. Given that the protection
            // is largely developer convenience, I'm ok with that tradeoff

            public override int Next()
            {
                this.VerifyThread();
                return base.Next();
            }

            public override int Next(int maxValue)
            {
                this.VerifyThread();
                return base.Next(maxValue);
            }

            public override int Next(int minValue, int maxValue)
            {
                this.VerifyThread();
                return base.Next(minValue, maxValue);
            }

            public int UnsafeNext(int minValue, int maxValue)
            {
                return base.Next(minValue, maxValue);
            }

            public override void NextBytes(byte[] buffer)
            {
                this.VerifyThread();
                base.NextBytes(buffer);
            }

            public override double NextDouble()
            {
                this.VerifyThread();
                return base.NextDouble();
            }

            public double UnsafeNextDouble()
            {
                return base.NextDouble();
            }

            private double? nextNextGaussian;

            double INextGaussianRandom.NextGaussian()
            {
                this.VerifyThread();

                if (this.nextNextGaussian.HasValue)
                {
                    var result = this.nextNextGaussian.Value;
                    this.nextNextGaussian = null;
                    return result;
                }

                double next, nextNext;
                this.NextTwoGaussians(out next, out nextNext);
                this.nextNextGaussian = nextNext;
                return next;
            }

            private void VerifyThread()
            {
                if (Thread.CurrentThread != this.thread)
                {
                    throw new InvalidOperationException($"Cannot use thread-local random from thread {this.thread.ManagedThreadId} on thread {Thread.CurrentThread.ManagedThreadId}");
                }
            }
        }
        #endregion

        #region ---- Factory ---- 
        // TODO look at how java does this
        public static Random Create()
        {
            var combinedSeed = unchecked((31 * Environment.TickCount) + Current.Next());
            return new Random(combinedSeed);
        }
        #endregion

        #region ---- Stream Interop ----
        public static Random FromStream(Stream randomBytes)
        {
            return new StreamRandomNumberGenerator(randomBytes).AsRandom();
        }

        [NullGuard(ValidationFlags.Arguments)]
        private sealed class StreamRandomNumberGenerator : RandomNumberGenerator
        {
            private readonly Stream stream;

            internal StreamRandomNumberGenerator(Stream stream)
            {
                this.stream = stream;
            }

            public override void GetBytes(byte[] data)
            {
                var bytesReadBeforeLastSeek = 0;
                var bytesRead = 0;
                while (bytesRead < data.Length)
                {
                    // based on StreamReader.ReadBlock. We don't want to
                    // give up until the end of the file is reached

                    var nextBytesRead = this.stream.Read(data, offset: bytesRead, count: data.Length - bytesRead);
                    if (nextBytesRead == 0) // eof
                    {
                        if (!this.stream.CanSeek)
                        {
                            throw new InvalidOperationException("Cannot produce additional random bytes because the given stream is exhausted and does not support seeking");
                        }
                        if (bytesReadBeforeLastSeek == 0)
                        {
                            // prevents us from going into an infinite loop seeking back to the beginning of an empty stream
                            throw new InvalidOperationException("Cannot produce additional random bytes because the given stream is empty");
                        }

                        // reset the stream
                        this.stream.Seek(0, SeekOrigin.Begin);
                        bytesReadBeforeLastSeek = bytesRead;
                    }
                    else
                    {
                        bytesRead += nextBytesRead;
                    }
                }
            }
        }
        #endregion
        
        #region ---- NextBits Random ----
        [NullGuard(ValidationFlags.Arguments)] // for NextBytes
        private abstract class NextBitsRandom : Random, INextGaussianRandom
        {
            // pass through the seed just in case
            protected NextBitsRandom(int seed) : base(seed) { }

            internal abstract int NextBits(int bits);

            #region ---- .NET Random Methods ----
            public sealed override int Next()
            {
                return this.Next(int.MaxValue);
            }

            public sealed override int Next(int maxValue)
            {
                // see remarks for this special case in the docs:
                // https://msdn.microsoft.com/en-us/library/zd1bc8e5%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
                if (maxValue == 0)
                {
                    return 0;
                }
                if (maxValue <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxValue), $"{nameof(maxValue)} must be positive. ");
                }

                unchecked
                {
                    if ((maxValue & -maxValue) == maxValue)  // i.e., bound is a power of 2
                    {
                        return (int)((maxValue * (long)this.NextBits(31)) >> 31);
                    }

                    int bits, val;
                    do
                    {
                        bits = this.NextBits(31);
                        val = bits % maxValue;
                    } while (bits - val + (maxValue - 1) < 0);
                    return val;
                }
            }

            public sealed override int Next(int minValue, int maxValue)
            {
                if (minValue == maxValue)
                {
                    return minValue;
                }
                if (minValue > maxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(minValue), $"{nameof(minValue)} ({minValue}) must not be > {nameof(maxValue)} ({maxValue})");
                }

                var range = (long)maxValue - minValue;

                // if the range is small, we can use Next(int)
                if (range <= int.MaxValue)
                {
                    return minValue + this.Next(maxValue: (int)range);
                }

                // otherwise, we use java's implementation for 
                // nextLong(long, long)
                var r = this.NextInt64();
                var m = range - 1;

                // power of two
                if ((range & m) == 0L)
                {
                    r = (r & m);
                }
                else
                {
                    // reject over-represented candidates
                    for (
                        var u = unchecked((long)((ulong)r >> 1)); // ensure non-negative
                        u + m - (r = u % range) < 0; // rejection check
                        u = unchecked((long)((ulong)this.NextInt64() >> 1)) // retry
                    ) ; 
                }

                return checked((int)(r + minValue));
            }

            public override void NextBytes(byte[] buffer)
            {
                for (int i = 0; i < buffer.Length;)
                {
                    for (int rand = this.NextInt32(), n = Math.Min(buffer.Length - i, 4);
                         n-- > 0; 
                         rand >>= 8)
                    {
                        buffer[i++] = unchecked((byte)rand);
                    }
                }
            }

            public sealed override double NextDouble()
            {
                return this.Sample();
            }

            protected sealed override double Sample()
            {
                return (((long)this.NextBits(26) << 27) + this.NextBits(27)) / (double)(1L << 53);
            }
            #endregion

            private double? nextNextGaussian;

            double INextGaussianRandom.NextGaussian()
            {
                if (this.nextNextGaussian.HasValue)
                {
                    var result = this.nextNextGaussian.Value;
                    this.nextNextGaussian = null;
                    return result;
                }

                double next, nextNext;
                this.NextTwoGaussians(out next, out nextNext);
                this.nextNextGaussian = nextNext;
                return next;
            }
        }
        #endregion

        #region ---- Java Random ----
        public static Random CreateJavaRandom()
        {
            // todo
            var seed = ((long)Environment.TickCount << 32) | (uint)ThreadLocalRandom.NextInt32();
            return CreateJavaRandom(seed);
        }

        public static Random CreateJavaRandom(long seed)
        {
            return new JavaRandom(seed);
        }

        private sealed class JavaRandom : NextBitsRandom
        {
            private long seed;

            public JavaRandom(long seed)
                // we shouldn't need the seed, but passing it through
                // just in case new Random() methods are added in the future
                // that don't call anything we've overloaded
                : base(unchecked((int)seed))
            {
                // this is based on "initialScramble()" in the Java implementation
                this.seed = (seed ^ 0x5DEECE66DL) & ((1L << 48) - 1);
            }

            internal override int NextBits(int bits)
            {
                unchecked
                {
                    this.seed = ((seed * 0x5DEECE66DL) + 0xBL) & ((1L << 48) - 1);
                    return (int)((ulong)this.seed >> (48 - bits));
                }
            }
        }
        #endregion

        #region ---- RandomNumberGenerator Interop ----
        public static Random AsRandom(this RandomNumberGenerator randomNumberGenerator)
        {
            return new RandomNumberGeneratorRandom(randomNumberGenerator);
        }

        [NullGuard(ValidationFlags.Arguments)]
        private sealed class RandomNumberGeneratorRandom : Random
        {
            private const int BufferLength = 512;

            private readonly RandomNumberGenerator rand;
            private readonly byte[] buffer = new byte[BufferLength];
            private int nextByteIndex;

            internal RandomNumberGeneratorRandom(RandomNumberGenerator randomNumberGenerator)
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
                if (minValue > maxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(minValue), $"{nameof(minValue)} ({minValue}) must be <= {nameof(maxValue)} ({maxValue}). ");
                }
                
                // this optimization for powers of two from https://docs.oracle.com/javase/8/docs/api/java/util/Random.html#nextInt-int-
                // We leave this outside of InternalNext() because Next() won't ever benefit from it
                var difference = (long)maxValue - minValue;
                unchecked
                {
                    if (difference <= int.MaxValue && (difference & -difference) == difference)
                    {
                        return minValue + (int)((difference * this.NextBits(31)) >> 31);
                    }
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

                var nextFourBytes = BitConverter.ToUInt32(this.buffer, this.nextByteIndex);
                this.nextByteIndex += 4;

                // the fact that we're uint here means we don't have to worry about sign-extending shift behavior
                var nextBits = nextFourBytes >> (32 - bits);

                return unchecked((int)nextBits);
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
