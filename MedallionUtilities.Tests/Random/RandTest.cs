using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion
{
    public class RandTest
    {
        [Fact]
        public void TestCreate()
        {
            var sequences = Enumerable.Range(0, 100)
                .Select(_ => Rand.Create())
                .Select(r => BitConverter.ToString(r.NextBytes().Take(10).ToArray()))
                .ToArray();

            Assert.Equal(actual: sequences.Distinct().Count(), expected: 100); 
        }

        [Fact]
        public void TestJavaCreate()
        {
            var sequences = Enumerable.Range(0, 100)
                .Select(_ => Rand.CreateJavaRandom())
                .Select(r => BitConverter.ToString(r.NextBytes().Take(10).ToArray()))
                .ToArray();

            Assert.Equal(actual: sequences.Distinct().Count(), expected: 100);
        }

        [Fact]
        public void TestDoubles()
        {
            var random1 = new System.Random(1);
            var random2 = new System.Random(1);
            Assert.Equal(
                actual: random1.NextDoubles().Take(10).ToArray(),
                expected: Enumerable.Range(0, 10).Select(_ => random2.NextDouble()).ToArray()
            );
        }

        [Fact]
        public void TestNextBoolean()
        {
            var random = new System.Random(1);
            var average = Enumerable.Range(0, 20000).Select(_ => Convert.ToInt32(random.NextBoolean()))
                .Average();
            (Math.Abs(average - .5) < .01).ShouldEqual(true);
        }

        [Fact]
        public void TestNextInt32()
        {
            var random = new System.Random(12345);
            var average = Enumerable.Range(0, 20000).Select(_ => Math.Sign(random.NextInt32()))
                .Average();
            Assert.Equal(actual: average, expected: 0, precision: 2);
        }

        [Fact]
        public void TestNextSingle()
        {
            var random = new System.Random(54321);
            var average = Enumerable.Range(0, 20000).Select(_ => random.NextSingle())
                .Average();
            Assert.Equal(actual: average, expected: .5f, precision: 2);
        }

        [Fact]
        public void TestNextGaussian()
        {
            var rand = new Random(1);

            var gaussians = Enumerable.Range(0, 10000).Select(_ => rand.NextGaussian()).ToArray();
            
            var average = gaussians.Average();
            var stdev = StandardDeviation(gaussians);

            Assert.True(Math.Abs(average) < 0.05, "was " + average);
            Assert.True(Math.Abs(stdev - 1) < 0.05, "was " + average);            
        }

        [Fact]
        public void TestCurrent()
        {
            var current = Rand.Current;
            Assert.Same(actual: Rand.Current, expected: current);
            var threadCurrent = Task.Run(() => Rand.Current).Result;
            Assert.Same(threadCurrent, current);
            current.Next(); // does not throw
        }

        [Fact]
        public void TestShuffled()
        {
            var shuffled = Enumerable.Range(0, 1000)
                .Shuffled(new Random(123456))
                .ToArray();
            var correlation = Correlation(shuffled.Select(Convert.ToDouble).ToArray(), Enumerable.Range(0, shuffled.Length).Select(Convert.ToDouble).ToArray());
            Assert.True(Math.Abs(correlation) < .05, correlation.ToString());
        }
        
        [Fact]
        public void TestShuffle()
        {
            var list = Enumerable.Range(0, 1000).ToList();
            list.Shuffle(new Random(654321));
            var correlation = Correlation(list.Select(Convert.ToDouble).ToArray(), Enumerable.Range(0, list.Count).Select(Convert.ToDouble).ToArray());
            Assert.True(Math.Abs(correlation) < .05, correlation.ToString());
        }

        [Fact]
        public void TestBoundedNextDouble()
        {
            Assert.Throws<ArgumentNullException>(() => default(Random).NextDouble(0));
            Assert.Throws<ArgumentNullException>(() => default(Random).NextDouble(0, 1));

            var rand = new Random(1);

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
            Assert.True(Math.Abs(average1 - 1.25) < 0.01, "was " + average1);

            var average2 = Enumerable.Range(0, 80000).Select(_ => rand.NextDouble(-10, 6.4)).Average();
            Assert.True(Math.Abs(average2 - -1.8) < 0.01, "was " + average2);
        }

        [Fact]
        public void TestStreamRandomChecks()
        {
            Assert.Throws<ArgumentNullException>(() => Rand.FromStream(default(Stream)));
            var disposedStream = new MemoryStream();
            disposedStream.Dispose();
            Assert.Throws<ArgumentException>(() => Rand.FromStream(disposedStream));

            var emptyStream = new MemoryStream();
            var emptyStreamRandom = Rand.FromStream(emptyStream);
            Assert.Throws<InvalidOperationException>(() => emptyStreamRandom.Next());

            var oneByteStream = new MemoryStream(new byte[] { 111 });
            var oneByteRandom = Rand.FromStream(oneByteStream);
            var bytes = new byte[128];
            oneByteRandom.NextBytes(bytes);
            Assert.Equal(actual: bytes, expected: Enumerable.Repeat((byte)111, bytes.Length));

            var noSeekStream = new NoSeekMemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            var noSeekRandom = Rand.FromStream(noSeekStream);
            BitConverter.GetBytes(noSeekRandom.NextInt64())
                .OrderBy(b => b)
                .SequenceShouldEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Assert.Throws<InvalidOperationException>(() => noSeekRandom.NextBytes(new byte[1]));
        }

        private class NoSeekMemoryStream : MemoryStream
        {
            public NoSeekMemoryStream(byte[] bytes) : base(bytes) { }

            public override bool CanSeek => false;
        }

        public static double Correlation(double[] a, double[] b)
        {
            var meanA = a.Average();
            var meanB = b.Average();

            var stdevA = StandardDeviation(a);
            var stdev2 = StandardDeviation(b);

            var val = a.Zip(b, (aa, bb) => ((aa - meanA) / stdevA) * ((bb - meanB) / stdev2))
                .Sum();
            return val / (a.Length - 1);
        }

        public static double StandardDeviation(double[] values)
        {
            var mean = values.Average();
            return Math.Sqrt(values.Sum(d => (d - mean) * (d - mean)) / (values.Length - 1));
        }
    }
}
