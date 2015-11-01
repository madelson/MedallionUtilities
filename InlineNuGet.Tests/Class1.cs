using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace InlineNuGet.Tests
{
    public class Class1
    {
        private readonly ITestOutputHelper output;

        public Class1(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Test()
        {
            var args = SyntaxFactory.ParseAttributeArgumentList("(\"a\")");
            this.output.WriteLine(args.ToFullString());
        }

        [Fact]
        public void Test2()
        {
            var tokens = SyntaxFactory.ParseTokens(@"#if !A
                    foo
                #endif
            ");
        }
    }
}
