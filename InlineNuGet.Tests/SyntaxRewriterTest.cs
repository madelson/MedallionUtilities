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

            var formatted = Microsoft.CodeAnalysis.Formatting.Formatter.Format(rewritten, new AdhocWorkspace()).ToFullString();
            this.output.WriteLine(formatted);

            var preprocessorSymbols = new[] { "LIB_PUBLIC", "LIB_DISABLE_EXTENSIONS", "LIB_USE_LOCAL_NAMESPACE" };
            var permutations = (long)Math.Pow(2, preprocessorSymbols.Length);
            for (var flags = 0; flags <= permutations; ++flags)
            {
                var symbols = preprocessorSymbols.Where((s, index) => (flags & (1L << index)) != 0);
                var parsed = SyntaxFactory.ParseCompilationUnit(formatted.Replace("$rootnamespace$", "My.Root.Namespace"), options: new CSharpParseOptions(preprocessorSymbols: symbols));
                if (parsed.ContainsDiagnostics)
                {
                    this.output.WriteLine($"{flags}: {string.Join(", ", symbols)}");
                    this.output.WriteLine(string.Join(Environment.NewLine, parsed.GetDiagnostics()));
                }
                Assert.Equal(actual: parsed.ContainsDiagnostics, expected: false);
            }
        }
    }
}
