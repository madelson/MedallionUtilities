using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    internal interface IParserNode
    {
        ParserNodeKind Kind { get; }
    }

    internal enum ParserNodeKind
    {
        ParseRule,
        ParsePrefixSymbols,
        TokenLookahead,
        GrammarLookahead,
        Result,
        MapResult,
    }

    internal sealed class ParseRuleNode : IParserNode
    {
        public ParseRuleNode(Rule rule) : this(new PartialRule(rule)) { }

        public ParseRuleNode(PartialRule rule)
        {
            this.Rule = rule;
        }

        public PartialRule Rule { get; }
        public ParserNodeKind Kind => ParserNodeKind.ParseRule;

        public override string ToString() => $"Parse({this.Rule})";
    }

    internal sealed class ParseSymbolNode : IParserNode
    {
        public ParseSymbolNode(IEnumerable<Symbol> prefixSymbols, IParserNode suffixNode)
        {
            this.PrefixSymbols = prefixSymbols.ToArray();
            this.SuffixNode = suffixNode;
        }

        public IReadOnlyList<Symbol> PrefixSymbols { get; }
        public IParserNode SuffixNode { get; }
        public ParserNodeKind Kind => ParserNodeKind.ParsePrefixSymbols;

        public override string ToString() => $"Parse({string.Join(", ", this.PrefixSymbols)}), {this.SuffixNode}";
    }

    internal sealed class TokenLookaheadNode : IParserNode
    {
        public TokenLookaheadNode(IEnumerable<KeyValuePair<Token, IParserNode>> mapping)
        {
            this.Mapping = mapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public IReadOnlyDictionary<Token, IParserNode> Mapping { get; }
        public ParserNodeKind Kind => ParserNodeKind.TokenLookahead;

        public override string ToString() => string.Join(" | ", this.Mapping.Select(kvp => $"{kvp.Key} => {kvp.Value}"));
    }

    internal sealed class GrammarLookaheadNode : IParserNode
    {
        public GrammarLookaheadNode(Token token, NonTerminal discriminator, IEnumerable<KeyValuePair<Rule, IParserNode>> mapping)
        {
            this.Token = token;
            this.Discriminator = discriminator;
            this.Mapping = mapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public Token Token { get; }
        public NonTerminal Discriminator { get; }
        public IReadOnlyDictionary<Rule, IParserNode> Mapping { get; }
        public ParserNodeKind Kind => ParserNodeKind.GrammarLookahead;

        public override string ToString() => $"{this.Token}, Parse({this.Discriminator}) {{ {string.Join(", ", this.Mapping.Select(kvp => $"{kvp.Key} => {kvp.Value}"))} }}";
    }

    internal sealed class ResultNode : IParserNode
    {
        public ResultNode(Rule rule)
        {
            this.Rule = rule;
        }

        public Rule Rule { get; }
        public ParserNodeKind Kind => ParserNodeKind.Result;
    }

    internal sealed class MapResultNode : IParserNode
    {
        public MapResultNode(IParserNode mapped, IEnumerable<KeyValuePair<Rule, Rule>> mapping)
        {
            this.Mapped = mapped;
            this.Mapping = mapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public IParserNode Mapped { get; }
        public IReadOnlyDictionary<Rule, Rule> Mapping { get; }
        public ParserNodeKind Kind => ParserNodeKind.MapResult;

        public override string ToString() => $"Map {{ {string.Join(", ", this.Mapping.Select(kvp => $"{kvp.Key} => {kvp.Value}"))} }}";
    }
}
