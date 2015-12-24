using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion.Random
{
    public class WeightedCoinFlipTest
    {
        [Fact]
        public void TestSystemRandom() => this.Test(new System.Random(int.MaxValue));

        [Fact]
        public void TestCryptoRandom()
        {
            using (var cryptoRandom = new RNGCryptoServiceProvider())
            {
                this.Test(cryptoRandom.AsRandom());
            }
        }

        [Fact]
        public void TestCurrentRandom() => this.Test(Rand.Current);

        [Fact]
        public void TestStreamRandom()
        {
            var bytes = new byte[12345];
            Rand.Current.NextBytes(bytes);
            this.Test(Rand.FromStream(new MemoryStream(bytes)));
        }

        private void Test(System.Random random)
        {
            const int Trials = 40000;

            foreach (var probability in new[] { .1, .3, .5, .7, .9 })
            {
                var count = 0;
                for (var i = 0; i < Trials; ++i)
                {
                    if (random.NextBoolean(probability))
                    {
                        ++count;
                    }
                }

                var observedProbability = count / (double)Trials;
                Assert.Equal(actual: observedProbability, expected: probability, precision: 1);
            }
        }
    }
}
