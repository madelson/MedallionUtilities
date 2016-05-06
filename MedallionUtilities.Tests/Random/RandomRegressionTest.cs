using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion
{
    public abstract class RandomRegressionTest
    {
        private readonly int[] nexts;
        private readonly int[] next10s;
        private readonly int[] next100To200s;
        private readonly bool[] nextBooleans;
        private readonly bool[] nextBoolean25s;
        private readonly byte[] nextBytes;
        private readonly double[] nextDoubles;
        private readonly double[] nextDouble10s;
        private readonly double[] nextDouble100To200s;
        private readonly double[] nextGaussians;
        private readonly int[] nextInt32s;
        private readonly long[] nextInt64s;
        private readonly float[] nextSingles;

        public RandomRegressionTest(
            int[] nexts,
            int[] next10s,
            int[] next100To200s,
            bool[] nextBooleans,
            bool[] nextBoolean25s,
            byte[] nextBytes,
            double[] nextDoubles,
            double[] nextDouble10s,
            double[] nextDouble100To200s,
            double[] nextGaussians,
            int[] nextInt32s,
            long[] nextInt64s,
            float[] nextSingles)
        {
            this.nexts = nexts;
            this.next10s = next10s;
            this.next100To200s = next100To200s;
            this.nextBooleans = nextBooleans;
            this.nextBoolean25s = nextBoolean25s;
            this.nextBytes = nextBytes;
            this.nextDoubles = nextDoubles;
            this.nextDouble10s = nextDouble10s;
            this.nextDouble100To200s = nextDouble100To200s;
            this.nextGaussians = nextGaussians;
            this.nextInt32s = nextInt32s;
            this.nextInt64s = nextInt64s;
            this.nextSingles = nextSingles;
        }

        protected abstract Random CreateRandom();

        [Fact]
        public void TestNext() => this.Validate(r => r.Next(), this.nexts);

        [Fact]
        public void TestNext10() => this.Validate(r => r.Next(10), this.next10s);

        [Fact]
        public void TestNext100To200() => this.Validate(r => r.Next(100, 200), this.next100To200s);

        [Fact]
        public void TestNextBoolean() => this.Validate(r => r.NextBoolean(), this.nextBooleans);

        [Fact]
        public void TestNextBoolean25() => this.Validate(r => r.NextBoolean(.25), this.nextBoolean25s);

        [Fact]
        public void TestNextBytes()
        {
            var buffer = new byte[1];
            this.Validate(r => { r.NextBytes(buffer); return buffer[0]; }, this.nextBytes);
        }

        [Fact]
        public void TestNextDouble() => this.Validate(r => r.NextDouble(), this.nextDoubles);

        [Fact]
        public void TestNextDouble10() => this.Validate(r => r.NextDouble(10), this.nextDouble10s);

        [Fact]
        public void TestNextDouble100To200() => this.Validate(r => r.NextDouble(100, 200), this.nextDouble100To200s);

        [Fact]
        public void TestNextGaussian() => this.Validate(r => r.NextGaussian(), this.nextGaussians);

        [Fact]
        public void TestNextInt32() => this.Validate(r => r.NextInt32(), this.nextInt32s);

        [Fact]
        public void TestNextInt64() => this.Validate(r => r.NextInt64(), this.nextInt64s);

        [Fact]
        public void TestNextSingle() => this.Validate(r => r.NextSingle(), this.nextSingles);

        private void Validate<T>(Func<Random, T> next, T[] expected)
        {
            var isDouble = typeof(T) == typeof(double) || typeof(T) == typeof(float);

            var random = this.CreateRandom();
            for (var i = 0; i < expected.Length; ++i)
            {
                var actual = next(random);
                if (!EqualityComparer<T>.Default.Equals(actual, expected[i])
                    && (!isDouble || Math.Abs(Convert.ToDouble(actual) - Convert.ToDouble(expected[i])) > 1e-6))
                {
                    var anotherRandom = this.CreateRandom();
                    Assert.True(false, $"Difference found at index {i} ({actual} vs {expected[i]}). Full actual was: \r\n new{(typeof(T) == typeof(byte) ? " byte" : string.Empty)}[] {{ {string.Join(", ", Enumerable.Range(0, expected.Length).Select(_ => next(anotherRandom).ToString().ToLowerInvariant() + (typeof(T) == typeof(float) ? "f" : string.Empty)))} }}");
                }
            }
        }
    }
}
