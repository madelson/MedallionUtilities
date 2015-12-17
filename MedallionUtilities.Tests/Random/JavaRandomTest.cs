using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Medallion.Random
{
    public class JavaRandomTest
    {
        #region ---- Overlapping Methods ----
        [Fact]
        public void TestNext()
        {
            var random = Rand.CreateJavaRandom(long.MaxValue);
            var values = Enumerable.Range(0, 100)
                .Select(_ => random.Next())
                .ToArray();
            Assert.True(
                values.SequenceEqual(new[] { 577549913, 943952225, 26349579, 1176895439, 1421815604, 1290198438, 894294477, 857465478, 1774177765, 121412731, 1730951808, 665220512, 121954317, 149520196, 176890342, 1481972250, 1552485069, 1938353972, 1201094686, 1570944655, 1286832617, 754206257, 1997687263, 975495143, 1335117720, 720466900, 499582545, 6265644, 378035946, 325038220, 1742348424, 1490692733, 512654038, 226558178, 232543901, 1495693083, 1280529375, 753494743, 2029142123, 617859745, 470117987, 185130857, 170887139, 1121888592, 1605548928, 928184884, 923208973, 1882898312, 275717795, 668457270, 1863258011, 397472851, 388596142, 785608363, 1710233643, 1067111196, 847626693, 426561303, 818346778, 1210424957, 837574326, 246750562, 1814949631, 1272610358, 535131907, 1544410909, 891703499, 1968069177, 1298348684, 1201169122, 987516795, 1999836385, 1836621277, 1984750298, 56056183, 682423875, 1874573330, 1207201113, 356369803, 703216285, 1809831267, 1470295459, 444584319, 161994220, 1233774672, 227479250, 2057678932, 44932761, 883073898, 1533719247, 2034106910, 964505939, 588522939, 1959040894, 690335609, 26345464, 26592211, 2040785910, 767017641, 1271274924, }),
                string.Join(",", values)
            );
        }

        [Fact]
        public void TestNextWithBound()
        {
            var random = Rand.CreateJavaRandom(9000000000L);
            var values = Enumerable.Range(1, count: 100)
                .Select(i => random.Next(i))
                .ToArray();
            Assert.True(
                values.SequenceEqual(new[] { 0, 0, 2, 2, 4, 1, 6, 7, 1, 5, 4, 4, 9, 6, 5, 1, 13, 3, 0, 3, 19, 21, 2, 18, 0, 15, 23, 25, 16, 9, 0, 30, 32, 14, 13, 9, 33, 20, 14, 19, 8, 30, 31, 14, 43, 16, 45, 26, 1, 41, 24, 18, 24, 38, 37, 17, 36, 8, 21, 16, 6, 60, 44, 30, 50, 57, 8, 19, 52, 51, 33, 58, 12, 66, 49, 74, 41, 30, 6, 56, 41, 9, 1, 7, 17, 54, 15, 29, 69, 69, 35, 14, 46, 91, 70, 80, 95, 12, 8, 46, }),
                string.Join(",", values)
            );
        }

        [Fact]
        public void TestNextDouble()
        {
            var random = Rand.CreateJavaRandom(12345);
            Assert.Equal(actual: random.NextDouble(), expected: 0.3618031071604718, precision: 15);
            Assert.Equal(actual: random.NextDouble(), expected: 0.932993485288541, precision: 15);
            Assert.Equal(actual: random.NextDouble(), expected: 0.8330913489710237, precision: 15);
        }

        [Fact]
        public void TestNextBytes()
        {
            var random = Rand.CreateJavaRandom(long.MinValue);
            var bytes = new byte[100];
            random.NextBytes(bytes);
            var sBytes = bytes.Select(b => unchecked((sbyte)b)).ToArray();
            Assert.True(
                sBytes.SequenceEqual(new sbyte[] { 96, -76, 32, -69, 56, 81, -39, -44, 122, -53, -109, 61, -66, 112, 57, -101, -10, -55, 45, -93, 58, -16, 29, 79, -73, 112, -23, -116, 3, 37, -12, 29, 62, -70, -8, -104, 109, -89, 18, -56, 43, -51, 77, 85, 75, -16, -75, 64, 35, -62, -101, 98, 77, -23, -17, -100, 47, -109, 30, -4, 88, 15, -102, -5, 8, 27, 18, -31, 7, -79, -24, 5, -14, -76, -11, -16, -15, -48, 12, 45, 15, 98, 99, 70, 112, -110, 28, 80, 88, 103, -1, 32, -10, -88, 51, 94, -104, -81, -121, 37 }),
                string.Join(",", sBytes)
            );
        }
        #endregion
    }
}
