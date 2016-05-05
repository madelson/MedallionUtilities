using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion
{
    public abstract class RandomTest
    {
        protected abstract Random GetRandom();

        [Fact]
        public void TestBirthdayParadox()
        {
            var random = this.GetRandom();
            new BirthdayParadoxProblem(sampleSize: 23, daysPerYear: 365)
                .AssertSimulationIsAccurate(() => random.Next(365));
        }

        [Fact]
        public void TestBirthdayParadoxProblemWithBytes()
        {
            var random = this.GetRandom();
            var byteStream = random.NextBytes().GetEnumerator();
            new BirthdayParadoxProblem(10, daysPerYear: 256).AssertSimulationIsAccurate(() => { byteStream.MoveNext(); return byteStream.Current; });
        }

        [Fact]
        public void TestWeightedCoinFlipArguments()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.NextBoolean(null, .5));

            var random = this.GetRandom();

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextBoolean(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextBoolean(double.NegativeInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextBoolean(-.001));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextBoolean(1.001));
            Assert.Throws<ArgumentException>(() => random.NextBoolean(double.NaN));

            random.NextBoolean(0).ShouldEqual(false);
            random.NextBoolean(1).ShouldEqual(true);
        }

        [Fact]
        public void TestWeightedCoinFlip() => WeightedCoinFlipTest.Test(this.GetRandom());

        [Fact]
        public void TestNextBoolean()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.NextBoolean(null));

            var random = this.GetRandom();
            
            var average = Enumerable.Range(0, 20000).Select(_ => Convert.ToInt32(random.NextBoolean()))
                .Average();
            Assert.True(Math.Abs(average - .5) < .01, $"was {average}");
        }

        [Fact]
        public void TestNext()
        {
            var random = this.GetRandom();

            var sum = 0L;
            const int Trials = 80000;
            for (var i = 0; i < Trials; ++i)
            {
                var next = random.Next();
                Assert.True(next >= 0);
                Assert.True(next < int.MaxValue);
                sum += next;
            }

            var average = sum / (double)Trials;
            var expected = (int.MaxValue - 1) / 2.0;
            var error = (average - expected) / expected;
            Assert.True(Math.Abs(error) < 0.01, $"was {average} (error = {error})");
        }

        [Fact]
        public void TestNextWithOneBound()
        {
            var random = this.GetRandom();

            random.Next(0).ShouldEqual(0);
            random.Next(1).ShouldEqual(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));

            var average = Enumerable.Range(2, 30000)
                .Sum(i => random.Next(i));
            var expected = Enumerable.Range(2, 30000).Sum(i => (i - 1) / 2.0);
            var error = (average - expected) / expected;
            Assert.True(Math.Abs(error) < 0.01, $"was {average} (error = {error})");
        }

        [Fact]
        public void TestNextInt32()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.NextInt32(null));

            var random = this.GetRandom();
            var average = Enumerable.Range(0, 30000).Select(_ => Math.Sign(random.NextInt32()))
                .Average();
            Assert.True(Math.Abs(average) < .01, $"was {average}");
        }

        [Fact]
        public void TestNextInt64()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.NextInt64(null));

            var random = this.GetRandom();
            var bitSetCounts = new int[64];
            const int Trials = 20000;
            for (var i = 0; i < Trials; ++i)
            {
                var next = random.NextInt64();
                for (var bit = 0; bit < bitSetCounts.Length; ++bit)
                {
                    if ((next & (1L << bit)) != 0) { bitSetCounts[bit]++; }
                }
            }

            Assert.True(Math.Abs(bitSetCounts.Select(c => c / (double)Trials).Average() - .5) < .01, $"was {string.Join(", ", bitSetCounts)}");
            for (var bit = 0; bit < bitSetCounts.Length; ++bit)
            {
                Assert.True(Math.Abs((bitSetCounts[bit] / (double)Trials) - .5) < .1, $"bit {bit} was {bitSetCounts[bit]} / {Trials}");
            }
        }

        [Fact]
        public void TestNextSingle()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.NextSingle(null));

            var random = this.GetRandom();
            var average = Enumerable.Range(0, 20000).Select(_ => random.NextSingle())
                .Average();
            Assert.Equal(actual: average, expected: .5f, precision: 2);
        }

        [Fact]
        public void TestNextDouble()
        {
            var random = this.GetRandom();
            var average = Enumerable.Range(0, 20000).Select(_ => random.NextDouble())
                .Average();
            Assert.Equal(actual: average, expected: .5, precision: 2);
        }

        [Fact]
        public void TestNextGaussian()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.NextGaussian(null));

            var rand = this.GetRandom();

            var gaussians = Enumerable.Range(0, 10000).Select(_ => rand.NextGaussian()).ToArray();

            var average = gaussians.Average();
            var stdev = RandTest.StandardDeviation(gaussians);

            Assert.True(Math.Abs(average) < 0.05, "was " + average);
            Assert.True(Math.Abs(stdev - 1) < 0.05, "was " + average);
        }

        [Fact]
        public void TestShuffled()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.Shuffled<int>(null, this.GetRandom()));

            Enumerable.Range(0, 100)
                .Shuffled(this.GetRandom())
                .OrderBy(i => i)
                .SequenceShouldEqual(Enumerable.Range(0, 100));

            var shuffled = Enumerable.Range(0, 2000)
                .Shuffled(this.GetRandom())
                .ToArray();
            var correlation = RandTest.Correlation(shuffled.Select(Convert.ToDouble).ToArray(), Enumerable.Range(0, shuffled.Length).Select(Convert.ToDouble).ToArray());
            Assert.True(Math.Abs(correlation) < .05, correlation.ToString());
        }

        [Fact]
        public void TestShuffle()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.Shuffle<int>(null, this.GetRandom()));

            var numbers = Enumerable.Range(0, 100).ToList();
            numbers.Shuffle(this.GetRandom());
            numbers.Sort();
            numbers.SequenceShouldEqual(Enumerable.Range(0, 100));

            var list = Enumerable.Range(0, 2000).ToList();
            list.Shuffle(this.GetRandom());
            var correlation = RandTest.Correlation(list.Select(Convert.ToDouble).ToArray(), Enumerable.Range(0, list.Count).Select(Convert.ToDouble).ToArray());
            Assert.True(Math.Abs(correlation) < .05, correlation.ToString());
        }

        [Fact]
        public void TestBoundedNextDouble()
        {
            Assert.Throws<ArgumentNullException>(() => default(Random).NextDouble(0));
            Assert.Throws<ArgumentNullException>(() => default(Random).NextDouble(0, 1));

            var rand = this.GetRandom();

            Assert.Throws<ArgumentOutOfRangeException>(() => rand.NextDouble(-double.Epsilon));
            Assert.Throws<ArgumentException>(() => rand.NextDouble(double.NaN));
            Assert.Throws<ArgumentException>(() => rand.NextDouble(double.PositiveInfinity));

            Assert.Throws<ArgumentOutOfRangeException>(() => rand.NextDouble(2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rand.NextDouble(double.MinValue, double.MaxValue));
            Assert.Throws<ArgumentException>(() => rand.NextDouble(double.NaN, 1));
            Assert.Throws<ArgumentException>(() => rand.NextDouble(1, double.NaN));
            Assert.Throws<ArgumentException>(() => rand.NextDouble(double.NaN, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => rand.NextDouble(double.NegativeInfinity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rand.NextDouble(0, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => rand.NextDouble(double.NegativeInfinity, double.NegativeInfinity));

            rand.NextDouble(0).ShouldEqual(0);
            rand.NextDouble(0, 0).ShouldEqual(0);

            var average1 = Enumerable.Range(0, 40000).Select(_ => rand.NextDouble(2.5)).Average();
            Assert.True(Math.Abs(average1 - 1.25) < 0.05, "was " + average1);

            var average2 = Enumerable.Range(0, 80000).Select(_ => rand.NextDouble(-10, 6.4)).Average();
            Assert.True(Math.Abs(average2 - -1.8) < 0.05, "was " + average2);
        }
    }
}
