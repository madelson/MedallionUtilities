using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse.Tests
{
    class TreeListener : IParserListener
    {
        private readonly Stack<IParseTreeNode> nodes = new Stack<IParseTreeNode>();
        private readonly IReadOnlyDictionary<Rule, Rule> ruleMapping;

        public IParseTreeNode Root => this.nodes.Single();

        public TreeListener(IReadOnlyDictionary<Rule, Rule> ruleMapping = null)
        {
            this.ruleMapping = ruleMapping;
        }

        public void OnSymbolParsed(Symbol symbol, Rule rule)
        {
            if (rule == null)
            {
                this.nodes.Push(new LeafNode { Token = (Token)symbol });
            }
            else
            {
                if (this.ruleMapping != null)
                {
                    var mappedRule = this.ruleMapping[rule];
                    if (mappedRule != null)
                    {
                        this.OnRuleParsed(mappedRule);
                    }
                }
                else
                {
                    this.OnRuleParsed(rule);
                }
            }
        }

        private void OnRuleParsed(Rule rule)
        {
            var children = new IParseTreeNode[rule.Symbols.Count];
            for (var i = rule.Symbols.Count - 1; i >= 0; --i)
            {
                children[i] = this.nodes.Pop();
            }
            this.nodes.Push(new Node { NonTerminal = (NonTerminal)rule.Produced, Children = children });
        }

        private class LeafNode : IParseTreeNode
        {
            public Token Token { get; set; }

            public Symbol Symbol => this.Token;
            public IReadOnlyList<IParseTreeNode> Children => Medallion.Collections.Empty.Array<IParseTreeNode>();

            public IParseTreeNode Flatten() => this;

            public override string ToString() => this.Token.Name;
        }

        private class Node : IParseTreeNode
        {
            public NonTerminal NonTerminal { get; set; }

            public Symbol Symbol => this.NonTerminal;
            public IReadOnlyList<IParseTreeNode> Children { get; set; }

            public IParseTreeNode Flatten()
            {
                if (!this.NonTerminal.Name.StartsWith("List<"))
                {
                    return new Node
                    {
                        NonTerminal = this.NonTerminal,
                        Children = this.Children.Select(c => c.Flatten()).ToArray(),
                    };
                }

                return new Node
                {
                    NonTerminal = this.NonTerminal,
                    Children = this.Children.Select(c => c.Flatten())
                        .SelectMany(c => c.Symbol == this.Symbol ? c.Children : new[] { c })
                        .ToArray()
                };
            }

            public override string ToString() => $"{this.NonTerminal.Name}({string.Join(", ", this.Children)})";
        }
    }

    interface IParseTreeNode
    {
        Symbol Symbol { get; }
        IReadOnlyList<IParseTreeNode> Children { get; }

        IParseTreeNode Flatten();
    }
}
