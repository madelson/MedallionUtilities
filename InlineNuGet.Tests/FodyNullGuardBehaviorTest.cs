using NullGuard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Medallion.Tools.InlineNuGet.Tests
{
    public class FodyNullGuardBehaviorTest
    {
        private readonly ITestOutputHelper output;

        public FodyNullGuardBehaviorTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void RemovesReference()
        {
            Assert.Equal(
                actual: this.GetType().Assembly.GetReferencedAssemblies()
                .SingleOrDefault(an => an.Name.Contains("NullGuard")),
                expected: null
            );                
        }

        [Fact]
        public void TestNullGuard()
        {
            var guarded = new NullGuarded();
            var ex = Assert.Throws<ArgumentNullException>(() => guarded.Foo(null));
            this.output.WriteLine(ex.Message);

            Assert.Throws<NullReferenceException>(() => guarded.Bar());
            Assert.Throws<NullReferenceException>(() => guarded.Internal(null));
            Assert.Throws<NullReferenceException>(() => guarded.Baz(null));
        }
    }

    [NullGuard(ValidationFlags.AllPublicArguments)]
    public class NullGuarded
    {
        public int Foo(string text)
        {
            return text.Length;
        }

        public int Bar(string text = null)
        {
            return text.Length;
        }

        internal int Internal(string text) => text.Length;

        public int Baz([AllowNull] string text = null)
        {
            return text.Length;
        }
    }
}
