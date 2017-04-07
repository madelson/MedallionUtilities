using Medallion;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Playground.BalancingTokenParse.Tests
{
    public class LeftRecursionRewriterTest
    {
        private static readonly Token ID = new Token("ID"),
            PLUS = new Token("+"),
            TIMES = new Token("*");

        private static readonly NonTerminal Start = new NonTerminal("Start"),
            Exp = new NonTerminal("Exp");

        private readonly ITestOutputHelper output;

        public LeftRecursionRewriterTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestAdditionAndMultiplicationRewrite()
        {
            var rules = new[]
            {
                new Rule(Start, Exp),

                new Rule(Exp, Exp, TIMES, Exp),
                new Rule(Exp, Exp, PLUS, Exp),
                new Rule(Exp, ID)
            };

            var rewritten = LeftRecursionRewriter.Rewrite(rules, ImmutableHashSet<Rule>.Empty);
            this.output.WriteLine(ToString(rewritten));

            var nodes = ParserBuilder.CreateParser(rewritten.Keys);
            var listener = new TreeListener(rewritten);
            var parser = new ParserNodeParser(nodes, Start);

            parser.Parse(new[] { ID, TIMES, ID, TIMES, ID, PLUS, ID, PLUS, ID, TIMES, ID }, listener);
            this.output.WriteLine(ToGroupedTokenString(listener.Root));
            ToGroupedTokenString(listener.Root)
                .ShouldEqual("(((ID * ID) * ID) + ID) + (ID * ID)");
        }

        private static string ToString(IReadOnlyDictionary<Rule, Rule> ruleMapping)
        {
            var grouped = ruleMapping.GroupBy(kvp => kvp.Key.Produced)
                .Select(
                    g => g.Key 
                        + " = "
                        + string.Join(string.Empty, g.Select(kvp => Environment.NewLine + "\t" + kvp.Key + (kvp.Key == kvp.Value ? string.Empty : " yields " + (kvp.Value == null ? "null" : kvp.Value.ToString()))))
                );
            return string.Join(Environment.NewLine + Environment.NewLine, grouped);
        }

        private static string ToGroupedTokenString(IParseTreeNode node)
        {
            var builder = new StringBuilder();
            ToGroupedTokenString(node.Flatten(), builder, mayNeedParens: false);
            return builder.ToString();
        }

        private static void ToGroupedTokenString(IParseTreeNode node, StringBuilder builder, bool mayNeedParens)
        {
            if (node.Symbol is Token)
            {
                builder.Append(node.Symbol.Name);
            }
            else
            {
                var writeParens = mayNeedParens && node.Children.Count > 1;
                if (writeParens) { builder.Append('('); }
                for (var i = 0; i < node.Children.Count; ++i)
                {
                    if (i > 0) { builder.Append(' '); }
                    ToGroupedTokenString(node.Children[i], builder, mayNeedParens: node.Children.Count > 1);
                }
                if (writeParens) { builder.Append(')'); }
            }
        }
    }
}
