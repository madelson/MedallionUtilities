using System;
using System.Collections.Generic;
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

            var gaussians1 = Enumerable.Range(0, 10000).Select(_ => rand.NextGaussian()).ToArray();
            var gaussians2 = rand.NextGaussians().Take(gaussians1.Length).ToArray();
            
            foreach (var gaussians in new[] { gaussians1, gaussians2 })
            {
                var average = gaussians.Average();
                var stdev = StandardDeviation(gaussians);

                Assert.True(Math.Abs(average) < 0.05, "was " + average);
                Assert.True(Math.Abs(stdev - 1) < 0.05, "was " + average);
            }            
        }

        [Fact]
        public void TestCurrent()
        {
            var current = Rand.Current;
            Assert.Same(actual: Rand.Current, expected: current);
            var threadCurrent = Task.Run(() => Rand.Current).Result;
            Assert.NotSame(threadCurrent, current);
            current.Next(); // does not throw
            Assert.Throws<InvalidOperationException>(() => threadCurrent.Next());
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

        private static double Correlation(double[] a, double[] b)
        {
            var meanA = a.Average();
            var meanB = b.Average();

            var stdevA = StandardDeviation(a);
            var stdev2 = StandardDeviation(b);

            var val = a.Zip(b, (aa, bb) => ((aa - meanA) / stdevA) * ((bb - meanB) / stdev2))
                .Sum();
            return val / (a.Length - 1);
        }

        private static double StandardDeviation(double[] values)
        {
            var mean = values.Average();
            return Math.Sqrt(values.Sum(d => (d - mean) * (d - mean)) / (values.Length - 1));
        }
    }
}
