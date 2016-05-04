using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion
{
    public class WeightedCoinFlipTest
    {
        [Fact]
        public void TestSystemRandom() => Test(new System.Random(int.MaxValue));

        [Fact]
        public void TestCryptoRandom()
        {
            using (var cryptoRandom = new RNGCryptoServiceProvider())
            {
                Test(cryptoRandom.AsRandom());
            }
        }

        [Fact]
        public void TestCurrentRandom() => Test(Rand.Current);

        [Fact]
        public void TestStreamRandom()
        {
            var bytes = new byte[12345];
            Rand.Current.NextBytes(bytes);
            Test(Rand.FromStream(new MemoryStream(bytes)));
        }

        [Fact]
        public void TestJavaRandom() => Test(Rand.CreateJavaRandom(1));

        public static void Test(System.Random random)
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
                (Math.Abs(observedProbability - probability) < .05).ShouldEqual(true);
            }
        }
    }
}
