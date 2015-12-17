using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Medallion.Enums
{
    public class EnumTest
    {
        [Fact]
        public void TestToString()
        {
            Assert.Equal("Compiled", Enum<RegexOptions>.ToString(RegexOptions.Compiled));
        }

        [Fact]
        public void TestIsFlags()
        {
            Assert.True(Enum<RegexOptions>.IsFlags);
            Assert.False(Enum<TestEnum>.IsFlags);
        }

        public enum TestEnum
        {
            A, B
        }
    }
}
