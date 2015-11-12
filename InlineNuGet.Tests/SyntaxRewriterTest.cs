using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Medallion.Tools;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis;

namespace Medallion.Tools.InlineNuGet.Tests
{
    public class SyntaxRewriterTest
    {
        private readonly ITestOutputHelper output;

        public SyntaxRewriterTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void RewriteTest()
        {
            var syntax = SyntaxFactory.ParseCompilationUnit(@"
namespace Foo.Tests {
    public static class Bar 
    {
        public static void Print<T>(this T @this) { }
    }

    internal interface Baz { }
}");
            var rewriter = new SyntaxRewriter("LIB", "Foo");
            var rewritten = rewriter.Visit(syntax);

            this.output.WriteLine(Microsoft.CodeAnalysis.Formatting.Formatter.Format(rewritten, new AdhocWorkspace()).ToFullString());
        }
    }
}
