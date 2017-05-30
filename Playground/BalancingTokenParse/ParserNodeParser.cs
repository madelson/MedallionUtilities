using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    class ParserNodeParser : IParser
    {
        private readonly IReadOnlyDictionary<NonTerminal, IParserNode> nodes;
        private readonly NonTerminal startSymbol;
        private readonly Action<string> debugWriter;
        private readonly Dictionary<string, Stack<bool>> stateVariables = new Dictionary<string, Stack<bool>>();

        private int index, lookaheadIndex;
        private IReadOnlyList<Token> tokens;
        private IParserListener listener;

        public ParserNodeParser(
            IReadOnlyDictionary<NonTerminal, IParserNode> nodes, 
            NonTerminal startSymbol,
            Action<string> debugWriter = null)
        {
            this.nodes = nodes;
            this.startSymbol = startSymbol;
            this.debugWriter = debugWriter ?? (s => { });
            this.stateVariables.Clear();
        }

        public void Parse(IReadOnlyList<Token> tokens, IParserListener listener)
        {
            this.index = 0;
            this.lookaheadIndex = -1;
            this.tokens = tokens.Concat(new[] { Token.Eof }).ToArray();
            this.listener = listener;

            this.Parse(this.startSymbol);
        }

        private Rule Parse(NonTerminal symbol)
        {
            var ruleUsed = this.Parse(this.nodes[symbol]);
            this.debugWriter($"FINISHED {ruleUsed}");
            if (!this.IsInLookahead)
            {
                this.listener.OnSymbolParsed(symbol, ruleUsed);
            }
            return ruleUsed;
        }

        private Rule Parse(IParserNode node)
        {
            this.debugWriter(
                $"{(this.IsInLookahead ? "LOOK " : "PARSE")} @{(this.IsInLookahead ? this.lookaheadIndex : this.index)}={this.Peek()}: {node.Kind} {node}"
            );

            switch (node.Kind)
            {
                case ParserNodeKind.ParseSymbol:
                    return this.Parse(((ParseSymbolNode)node).Symbol);
                case ParserNodeKind.ParseRule:
                    var rule = ((ParseRuleNode)node).Rule;

                    if (!this.IsInLookahead
                        && rule.Rule.RequiredParserVariable != null
                        && (
                            !this.stateVariables.TryGetValue(rule.Rule.RequiredParserVariable, out var values)
                            || values.Count == 0
                            || !values.Peek()
                        ))
                    {
                        throw new InvalidOperationException($"Cannot parse {rule.Rule} without variable {rule.Rule.RequiredParserVariable}");
                    }

                    foreach (var symbol in rule.Symbols)
                    {
                        if (symbol is Token) { this.Eat((Token)symbol); }
                        else { this.Parse((NonTerminal)symbol); }
                    }

                    if (rule.Rule.Action != null)
                    {
                        switch (rule.Rule.Action.Kind)
                        {
                            case RuleActionKind.Pop:
                                if (!this.stateVariables.TryGetValue(rule.Rule.Action.Variable, out var toPop) || toPop.Count == 0)
                                {
                                    throw new InvalidOperationException($"Rule {rule.Rule} should pop {rule.Rule.Action.Variable} but the stack is empty");
                                }
                                toPop.Pop();
                                break;
                            case RuleActionKind.Push:
                                Stack<bool> toPush;
                                if (!this.stateVariables.TryGetValue(rule.Rule.Action.Variable, out toPush))
                                {
                                    this.stateVariables.Add(rule.Rule.Action.Variable, toPush = new Stack<bool>());
                                }
                                toPush.Push(false);
                                break;
                            case RuleActionKind.Set:
                                if (!this.stateVariables.TryGetValue(rule.Rule.Action.Variable, out var toSet) || toSet.Count == 0)
                                {
                                    throw new InvalidOperationException($"Rule {rule.Rule} should set {rule.Rule.Action.Variable} but the stack is empty");
                                }
                                toSet.Pop();
                                toSet.Push(true);
                                break;
                            default:
                                throw new InvalidOperationException(rule.Rule.Action.Kind.ToString());
                        }
                    }

                    return rule.Rule;
                case ParserNodeKind.TokenLookahead:
                    var mapping = ((TokenLookaheadNode)node).Mapping;
                    var next = this.Peek();
                    return this.Parse(mapping[next]);
                case ParserNodeKind.MapResult:
                    var mapNode = (MapResultNode)node;
                    var innerResult = this.Parse(mapNode.Mapped);
                    return this.Parse(mapNode.Mapping[innerResult]);
                case ParserNodeKind.ParsePrefixSymbols:
                    var prefixNode = (ParsePrefixSymbolsNode)node;
                    foreach (var symbol in prefixNode.PrefixSymbols)
                    {
                        if (symbol is Token) { this.Eat((Token)symbol); }
                        else { this.Parse((NonTerminal)symbol); }
                    }
                    return this.Parse(prefixNode.SuffixNode);
                case ParserNodeKind.GrammarLookahead:
                    var lookaheadNode = (GrammarLookaheadNode)node;
                    if (this.IsInLookahead)
                    {
                        this.Eat(lookaheadNode.Token);
                        var ruleUsed = this.Parse(lookaheadNode.Discriminator);
                        return lookaheadNode.Mapping[ruleUsed];
                    }
                    else
                    {
                        this.lookaheadIndex = this.index;

                        this.Eat(lookaheadNode.Token);
                        var ruleUsed = this.Parse(lookaheadNode.Discriminator);

                        this.lookaheadIndex = -1;

                        return this.Parse(new ParseRuleNode(lookaheadNode.Mapping[ruleUsed]));
                    }
                default:
                    throw new ArgumentException(node.Kind.ToString());
            }
        }

        private bool IsInLookahead
        {
            get
            {
                if (this.lookaheadIndex < 0) { return false; }
                if (this.lookaheadIndex < this.index) { throw new InvalidOperationException($"bad state: index: {this.index}, lookahead: {this.lookaheadIndex}"); }
                return true;
            }
        }

        private Token Peek() => this.tokens[this.IsInLookahead ? this.lookaheadIndex : this.index];
        
        private void Eat(Token token)
        {
            this.debugWriter($"EAT {token}");

            var isInLookahead = this.IsInLookahead;
            var indexToUse = IsInLookahead ? this.lookaheadIndex : this.index;
            if (this.tokens[indexToUse] != token)
            {
                throw new Exception($"expected '{token}', found {this.tokens[indexToUse]}");
            }
            if (IsInLookahead)
            {
                ++this.lookaheadIndex;
            }
            else
            {
                this.listener.OnSymbolParsed(token, null);
                ++this.index;
            }
        }
    }
}
