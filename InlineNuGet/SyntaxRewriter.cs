using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace Medallion.Tools
{
    internal class SyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly string conditionalCompilationSymbolBaseName, baseNamespace;

        public SyntaxRewriter(
            string conditionalCompilationSymbolBaseName,
            string baseNamespace)
        {
            this.conditionalCompilationSymbolBaseName = conditionalCompilationSymbolBaseName;
            this.baseNamespace = baseNamespace;
        }

        #region ---- Type Declarations ---
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var updated = !node.Ancestors().OfType<TypeDeclarationSyntax>().Any()
                ? node.AddAttributeLists(GetTypeDeclarationAttributes())
                    .WithModifiers(this.GetTypeDeclarationModifiers(node.Modifiers))
                : node;
            return base.VisitClassDeclaration(updated);
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            var updated = !node.Ancestors().OfType<TypeDeclarationSyntax>().Any()
                ? node.AddAttributeLists(GetTypeDeclarationAttributes())
                    .WithModifiers(this.GetTypeDeclarationModifiers(node.Modifiers))
                : node;
            return base.VisitInterfaceDeclaration(updated);
        }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            var updated = !node.Ancestors().OfType<TypeDeclarationSyntax>().Any()
                ? node.AddAttributeLists(GetTypeDeclarationAttributes())
                    .WithModifiers(this.GetTypeDeclarationModifiers(node.Modifiers))
                : node;
            return base.VisitStructDeclaration(updated);
        }

        private SyntaxTokenList GetTypeDeclarationModifiers(SyntaxTokenList modifiers)
        {
            var result = modifiers;

            var @public = modifiers.FirstOrDefault(t => t.IsKind(SyntaxKind.PublicKeyword));
            if (@public != default(SyntaxToken))
            {
                var @internal = SyntaxFactory.ParseToken($@"
                        #if {this.conditionalCompilationSymbolBaseName}_PUBLIC
                            public
                        #else
                            internal
                    ");
                @internal = @internal.WithTrailingTrivia(@internal.TrailingTrivia.AddRange(SyntaxFactory.ParseTrailingTrivia("#endif\r\n")));
                result = result.Replace(@public, @internal);
            }

            if (!modifiers.Any(SyntaxKind.PartialKeyword))
            {
                result = result.Add(SyntaxFactory.ParseToken(" partial "));
            }

            return result;
        } 

        private static AttributeListSyntax GetTypeDeclarationAttributes()
        {
            var generatedCode = SyntaxFactory.Attribute(
                SyntaxFactory.ParseName("global::System.CodeDom.Compiler.GeneratedCodeAttribute"),
                SyntaxFactory.ParseAttributeArgumentList($"(\"Medallion.Tools.InlineNuGet\", \"{typeof(Program).Assembly.GetName().Version}\")")
            );

            var compilerGenerated = SyntaxFactory.Attribute(
                SyntaxFactory.ParseName("global::System.Diagnostics.DebuggerNonUserCodeAttribute")
            );

            return SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(new[] { generatedCode, compilerGenerated }));
        }
        #endregion

        #region ---- Extension Methods ----
        public override SyntaxNode VisitParameter(ParameterSyntax node)
        {
            ParameterSyntax updated;
            if (node.Modifiers.Any(SyntaxKind.ThisKeyword))
            {
                var @this = node.Modifiers.First(t => t.IsKind(SyntaxKind.ThisKeyword));
                var optionalThis = SyntaxFactory.ParseToken($@"
                    #if !{this.conditionalCompilationSymbolBaseName}_DISABLE_EXTENSIONS
                        this
                ");
                optionalThis = optionalThis.WithTrailingTrivia(optionalThis.TrailingTrivia.AddRange(SyntaxFactory.ParseTrailingTrivia("#endif\r\n")));
                updated = node.WithModifiers(node.Modifiers.Replace(@this, optionalThis)); 
            }
            else
            {
                updated = node;
            }

            return base.VisitParameter(updated);
        }
        #endregion

        #region ---- Namespace ----
        public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var @namespace = Regex.Replace(node.Name.ToFullString(), @"\s+", string.Empty);
            NamespaceDeclarationSyntax updated;
            if (@namespace.StartsWith(this.baseNamespace, StringComparison.Ordinal))
            {
                var name = SyntaxFactory.ParseName($@"
                    #if {this.conditionalCompilationSymbolBaseName}_USE_LOCAL_NAMESPACE
                        $rootnamespace${@namespace.Substring(this.baseNamespace.Length)}
                    #else
                        {node.Name.ToFullString()}
                ");
                name = name.WithTrailingTrivia(name.GetTrailingTrivia().AddRange(SyntaxFactory.ParseTrailingTrivia("#endif\r\n")));
                updated = node.WithName(name);
            }
            else
            {
                updated = node;
            }

            return base.VisitNamespaceDeclaration(updated);
        }
        #endregion
    }
}
