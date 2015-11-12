using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Medallion.Tools.InlineNuGet.Tests
{
    public class CSharp6SyntaxRewriterTest
    {
        private readonly ITestOutputHelper output;

        public CSharp6SyntaxRewriterTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void SimpleTest()
        {
            var compilationUnit = SyntaxFactory.ParseCompilationUnit(@"
                namespace Foo {
                    public class A {
                        public string X(int a) { return nameof(a) + $""_{a, 1:0.0}_"" + $@""_{100}_{200, 0}abc"" + $""xyz""; }

                        public void Y() { }

                        public void Z() => this.Y();

                        public int Val() => 2;
                    }
                }"
            );
            var compilation = CSharpCompilation.Create("Foo", new[] { compilationUnit.SyntaxTree }, new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
            var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

            var rewriter = new CSharp6SyntaxRewriter(model);
            var visited = rewriter.Visit(compilationUnit);

            this.output.WriteLine(visited.ToFullString());

            var parsed = SyntaxFactory.ParseCompilationUnit(visited.ToFullString(), options: new CSharpParseOptions(LanguageVersion.CSharp5));
            Assert.Equal(actual: parsed.ContainsDiagnostics, expected: false);
        }
    }
}
