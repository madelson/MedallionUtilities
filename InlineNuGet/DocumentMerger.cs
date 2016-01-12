using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Tools
{
    internal class DocumentMerger
    {
        public static CompilationUnitSyntax Merge(IReadOnlyCollection<CompilationUnitSyntax> documentSyntaxes)
        {
            var merged = SyntaxFactory.CompilationUnit(
                externs: SyntaxFactory.List(documentSyntaxes.SelectMany(n => n.Externs)),
                usings: SyntaxFactory.List(MergeAndSortUsings(documentSyntaxes.SelectMany(n => n.Usings))),
                attributeLists: SyntaxFactory.List(documentSyntaxes.SelectMany(n => n.AttributeLists)),
                members: SyntaxFactory.List(documentSyntaxes.SelectMany(n => n.Members).ToArray()),
                endOfFileToken: documentSyntaxes.First().EndOfFileToken
            );

            return merged;
        }

        private static IEnumerable<UsingDirectiveSyntax> MergeAndSortUsings(IEnumerable<UsingDirectiveSyntax> usings)
        {
            var result = usings.GroupBy(u => u.ToString())
                .SelectMany(g => g.Take(1))
                .OrderBy(u => u.ToString());

            return result;
        }

        private static IEnumerable<MemberDeclarationSyntax> MergeMembers(IReadOnlyCollection<MemberDeclarationSyntax> members)
        {
            if (members.Any(m => !m.IsKind(SyntaxKind.NamespaceDeclaration)))
            {
                throw new NotSupportedException("All top-level constructs much be namespaces");
            }

            var namespaces = members.Cast<NamespaceDeclarationSyntax>();

            var result = namespaces.GroupBy(n => n.Name.ToString())
                .Select(g => g.First()
                    .WithExterns(SyntaxFactory.List(g.SelectMany(n => n.Externs)))
                    .WithUsings(SyntaxFactory.List(MergeAndSortUsings(g.SelectMany(n => n.Usings))))
                    .WithMembers(SyntaxFactory.List(g.SelectMany(n => n.Members)))
                );

            return result;
        }

        //private static IEnumerable<MemberDeclarationSyntax> MergeNamespaceMembers(IReadOnlyCollection<NamespaceDeclarationSyntax> namespaces)
        //{
        //}

        //private static TypeDeclarationSyntax MergePartialTypeDeclarations(IReadOnlyCollection<TypeDeclarationSyntax> typeDeclarations)
        //{
        //    if (typeDeclarations.Count == 1) { return typeDeclarations.Single(); }

        //    switch (typeDeclarations.First().Kind())
        //    {
        //        case SyntaxKind.ClassDeclaration:
        //            var nodes = typeDeclarations.Cast<ClassDeclarationSyntax>();
        //            return nodes.First().Update(
        //                SyntaxFactory.List(nodes.Select(n => n.att)))
        //            break;
        //        default:
        //            throw new InvalidOperationException($"Unexpected kind {typeDeclarations.First().Kind()}");
        //    }
        //}
    }
}
