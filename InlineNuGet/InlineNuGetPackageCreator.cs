using Medallion.Shell;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Medallion.Tools
{
    internal class InlineNuGetPackageCreator : IDisposable
    {
        private readonly Project project;
        private readonly string nuspec, outputDirectory;
        private readonly DirectoryInfo tempWorkspace;

        private InlineNuGetPackageCreator(string projectFilePath, string nuspecPath, string outputDirectory)
        {
            this.project = MSBuildWorkspace.Create().OpenProjectAsync(projectFilePath).Result;
            this.nuspec = nuspecPath;
            this.outputDirectory = outputDirectory;
            this.tempWorkspace = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "InlineNuGet_" + Guid.NewGuid()));
            this.tempWorkspace.Create();
        }

        public static List<string> Create(string projectFilePath, string nuspec, string outputDirectory)
        {
            using (var creator = new InlineNuGetPackageCreator(projectFilePath, nuspec, outputDirectory))
            {
                return creator.Create();
            } 
        }

        private List<string> Create()
        {
            var codeFile = Path.Combine(this.tempWorkspace.FullName, Path.GetFileNameWithoutExtension(this.project.FilePath) + ".cs.pp");

            var updatedNuspec = NuspecUpdater.RewriteNuspec(this.nuspec, this.project, codeFile);
            var tempNuspecPath = Path.Combine(this.tempWorkspace.FullName, Path.GetFileName(nuspec));
            File.WriteAllText(tempNuspecPath, updatedNuspec);

            Console.WriteLine($"Nuspec written to {tempNuspecPath}");

            var cSharpFiles = this.project.Documents.Where(d => d.SourceCodeKind == SourceCodeKind.Regular)
                .Where(d => d.Name != "AssemblyInfo.cs")
                // eliminate the weird auto-generated assembly attributes file
                .Where(d => d.FilePath.StartsWith(Path.GetDirectoryName(this.project.FilePath), StringComparison.OrdinalIgnoreCase));
            var syntaxRoots = cSharpFiles.Select(this.RewriteDocumentSyntax)
                .Cast<CompilationUnitSyntax>()
                // ignore files without at least one namespace
                .Where(n => n.Members.Any())
                .ToArray();

            var merged = DocumentMerger.Merge(syntaxRoots);
            var commented = HeaderCommentGenerator.AddHeaderComment(merged, nuspec: tempNuspecPath);
            var formatted = Formatter.Format(commented, new AdhocWorkspace());

            using (var writer = new StreamWriter(codeFile))
            {
                formatted.WriteTo(writer);
            }

            Console.WriteLine($"Code written to {codeFile}");

            var packCommand = Command.Run("NuGet.exe", new[] { "pack", tempNuspecPath, "-Symbols" }, options: o => o.ThrowOnError().WorkingDirectory(this.tempWorkspace.FullName));
            packCommand.StandardOutput.PipeToAsync(Console.Out);
            packCommand.StandardError.PipeToAsync(Console.Error);
            packCommand.Wait();

            var outputFiles = new List<string>();
            foreach (var packageFile in this.tempWorkspace.GetFiles("*.nupkg"))
            {
                var outputFile = Path.Combine(this.outputDirectory, Path.GetFileName(packageFile.FullName));
                File.Delete(outputFile);
                packageFile.MoveTo(outputFile);
                outputFiles.Add(outputFile);
            }
            return outputFiles;
        }

        private SyntaxNode RewriteDocumentSyntax(Document document)
        {
            var syntaxRoot = document.GetSyntaxRootAsync().Result;
            var cSharp6Rewriter = new CSharp6SyntaxRewriter(document.GetSemanticModelAsync().Result);
            var withoutCSharp6Constructs = cSharp6Rewriter.Visit(syntaxRoot);

            // verify
            var cSharp5Parsed = SyntaxFactory.ParseCompilationUnit(withoutCSharp6Constructs.ToFullString(), options: new CSharpParseOptions(LanguageVersion.CSharp5));
            if (cSharp5Parsed.ContainsDiagnostics)
            {
                throw new FormatException($"Code contained C#6 constructs with no supported translation: {string.Join(", ", cSharp5Parsed.GetDiagnostics().Select(d => d.ToString()))}");
            }

            // no better way to do this currently, see
            // http://stackoverflow.com/questions/15132175/reading-the-default-namespace-through-roslyn-api
            var rootNamespace = XDocument.Load(new StreamReader(this.project.FilePath))
                .Descendants()
                .Where(e => e.Name.LocalName == "RootNamespace")
                .Select(e => e.Value.Trim())
                .Single();

            var generalSyntaxRewriter = new SyntaxRewriter(
                conditionalCompilationSymbolBaseName: this.project.Name.Replace(".", "_"),
                baseNamespace: rootNamespace
            );
            var rewritten = generalSyntaxRewriter.Visit(withoutCSharp6Constructs);

            return rewritten;
        }

        void IDisposable.Dispose()
        {
            this.tempWorkspace.Delete(recursive: true);
        }
    }
}
