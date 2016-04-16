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
    public class BirthdayParadoxTest
    {
        [Fact]
        public void VerifyNativeRandom() => this.TestRandom(new System.Random(0));

        [Fact]
        public void TestCryptoRandom()
        {
            using (var random = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                this.TestRandom(random.AsRandom());
            }
        }

        [Fact]
        public void TestStreamRandom()
        {
            var bytes = new byte[BirthdayParadoxProblem.DefaultTrials * 2];
            new System.Random(0).NextBytes(bytes);
            var random = Rand.FromStream(new MemoryStream(bytes));
            this.TestRandom(random);
        }

        [Fact]
        public void TestJavaRandom() => this.TestRandom(Rand.CreateJavaRandom(123456789));

        [Fact]
        public void TestCurrentRandom() => this.TestRandom(Rand.Current);

        private void TestRandom(System.Random random)
        {
            new BirthdayParadoxProblem(23).AssertSimulationIsAccurate(() => random.Next(365));

            var byteStream = random.NextBytes().GetEnumerator();
            new BirthdayParadoxProblem(10, daysPerYear: 256).AssertSimulationIsAccurate(() => { byteStream.MoveNext(); return byteStream.Current; });
        }
    }

    public class BirthdayParadoxProblem
    {
        public const int DefaultTrials = 40000;

        public BirthdayParadoxProblem(int sampleSize, int daysPerYear = 365)
        {
            this.SampleSize = sampleSize;
            this.DaysPerYear = daysPerYear;
            this.Probability = 1.0 - Enumerable.Range(1, sampleSize - 1)
                .Select(i => (daysPerYear - i) / (double)daysPerYear)
                .Aggregate((a, b) => a * b);
        }

        public int SampleSize { get; }
        public int DaysPerYear { get; }
        public double Probability { get; }

        public double SimulateProbability(Func<int> dayPicker, int trials = DefaultTrials)
        {
            var birthdays = new HashSet<int>();
            var collisionCount = 0;
            for (var i = 0; i < trials; ++i)
            {
                birthdays.Clear();

                // one trial
                for (var j = 0; j < SampleSize; ++j)
                {
                    var birthday = dayPicker();
                    if (!birthdays.Add(birthday))
                    {
                        ++collisionCount;
                        break;
                    }
                }
            }

            var observedProbabilityOfAtLeastOneCollision = collisionCount / (double)trials;
            return observedProbabilityOfAtLeastOneCollision;
        }

        public void AssertSimulationIsAccurate(Func<int> dayPicker, int trials = DefaultTrials, double maxErrorRatio = 0.05)
        {
            var observed = this.SimulateProbability(dayPicker, trials);
            var errorRatio = Math.Abs(observed - this.Probability) / this.Probability;
            Assert.True(errorRatio <= maxErrorRatio, $"Error = {errorRatio} (observed = {observed}, calculated = {this.Probability})");
        }
    }
}
