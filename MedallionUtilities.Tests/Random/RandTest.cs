using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion.Random
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
            var average = Enumerable.Range(0, 10000).Select(_ => Convert.ToInt32(random.NextBoolean()))
                .Average();
            Assert.Equal(actual: average, expected: .5, precision: 2);
        }

        [Fact]
        public void TestNextInt32()
        {
            var random = new System.Random(12345);
            var average = Enumerable.Range(0, 20000).Select(_ => Math.Sign(random.NextInt32()))
                .Average();
            Assert.Equal(actual: average, expected: 0, precision: 2);
        }
    }
}
