using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Medallion.Tools
{
    // TODO new prop types (more annoying)
    // TODO ?. syntax and related (more annoying)

    internal class CSharp6SyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel model;

        public CSharp6SyntaxRewriter(SemanticModel model)
        {
            this.model = model;
        }

        private static readonly string NameOf = SyntaxFacts.GetText(SyntaxKind.NameOfKeyword);

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var firstToken = node.GetFirstToken();
            if (firstToken.Span.Length == NameOf.Length && firstToken.ToString() == NameOf)
            {
                var constantValue = this.model.GetConstantValue(node);
                if (constantValue.HasValue)
                {
                    return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal((string)constantValue.Value));
                }
            }

            return base.VisitInvocationExpression(node);
        }
        
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            MethodDeclarationSyntax updated;
            if (node.ExpressionBody != null)
            {
                var needsReturn = this.model.GetTypeInfo(node.ExpressionBody.Expression).Type.SpecialType != SpecialType.System_Void;

                var block = SyntaxFactory.Block(
                    needsReturn ? SyntaxFactory.ReturnStatement(node.ExpressionBody.Expression)
                        : (StatementSyntax)SyntaxFactory.ExpressionStatement(node.ExpressionBody.Expression)
                );

                updated = node.WithExpressionBody(null).WithSemicolonToken(default(SyntaxToken)).WithBody(block).NormalizeWhitespace();
            }
            else
            {
                updated = node;
            }

            return base.VisitMethodDeclaration(updated);
        }

        public override SyntaxNode VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            var expressions = new List<ExpressionSyntax>();
            var formatStringBuilder = new StringBuilder();

            if (node.StringStartToken.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken))
            {
                formatStringBuilder.Append('@');
            }
            formatStringBuilder.Append('"');

            foreach (var expression in node.Contents)
            {
                if (expression.IsKind(SyntaxKind.Interpolation))
                {
                    var interpolation = (InterpolationSyntax)expression;
                    expressions.Add(interpolation.Expression);

                    formatStringBuilder.Append('{')
                        .Append(expressions.Count - 1)
                        .Append(interpolation.AlignmentClause?.ToFullString())
                        .Append(interpolation.FormatClause?.ToFullString())
                        .Append('}');  
                }
                else if (expression.IsKind(SyntaxKind.InterpolatedStringText))
                {
                    formatStringBuilder.Append(expression.ToFullString());
                }
                else
                {
                    throw new NotSupportedException(expression.Kind().ToString());
                }
            }

            formatStringBuilder.Append('"');

            ExpressionSyntax result;
            if (expressions.Count == 0)
            {
                result = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.ParseToken(formatStringBuilder.ToString()));
            }
            else
            {
                result = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                        SyntaxFactory.IdentifierName("Format")
                    ),
                    SyntaxFactory.ParseArgumentList($"({formatStringBuilder}, {string.Join(", ", expressions.Select(e => e.ToFullString()))})")
                );
            }
            
            var resultWithTrivia = result
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());

            return this.Visit(resultWithTrivia);
        }
    }
}
