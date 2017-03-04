using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    class ParserGenerator
    {
        private readonly IReadOnlyList<Rule> rules;
        private readonly FirstFollowCalculator firstFollow;

        public ParserGenerator(IEnumerable<Rule> grammar)
        {
            this.rules = grammar.ToArray();
            this.firstFollow = new FirstFollowCalculator(this.rules);
        }

        public IParser Create()
        {
            var table = this.firstFollow.NonTerminals.ToDictionary(nt => nt, _ => new Dictionary<Token, Rule>());
            foreach (var rule in this.rules)
            {
                var subTable = table[rule.Produced];
                foreach (var token in this.NextOf(rule))
                {
                    if (subTable.ContainsKey(token))
                    {
                        throw new InvalidOperationException($"Conflict: cannot decide between {rule} and {subTable[token]} on {token}");
                    }
                    subTable.Add(token, rule);
                }
            }

            return new TableParser { StartSymbol = this.firstFollow.StartSymbol, Table = table };
        }

        private ISet<Token> NextOf(Rule rule)
        {
            var result = new HashSet<Token>();
            foreach (var symbol in rule.Symbols)
            {
                var firsts = this.firstFollow.First[symbol];
                foreach (var first in firsts)
                {
                    if (first != null) { result.Add(first); }
                }
                if (!firsts.Contains(null))
                {
                    return result; // not nullable
                }
            }

            // nullable: add the follow
            foreach (var follow in this.firstFollow.Follow[rule.Produced])
            {
                result.Add(follow);
            }

            return result;
        }

        private class TableParser : IParser
        {
            public NonTerminal StartSymbol { get; set; }
            public IReadOnlyDictionary<NonTerminal, Dictionary<Token, Rule>> Table { get; set; }

            private IReadOnlyList<Token> tokens;
            private IParserListener listener;
            private int index; 

            public void Parse(IReadOnlyList<Token> tokens, IParserListener listener)
            {
                this.tokens = tokens;
                this.listener = listener;
                this.index = 0;

                this.Parse(this.StartSymbol);
            }

            private void Parse(NonTerminal symbol)
            {
                Rule rule;
                if (!this.Table[symbol].TryGetValue(this.Peek(), out rule))
                {
                    throw new InvalidOperationException($"Could not determine a parse rule to apply for {symbol.Name} on seeing {this.Peek().Name}");
                }

                foreach (var childSymbol in rule.Symbols)
                {
                    if (childSymbol is Token) { this.Eat((Token)childSymbol); }
                    else { this.Parse((NonTerminal)childSymbol); }
                }

                this.listener.OnSymbolParsed(symbol, rule);
            }

            private Token Peek()
            {
                return this.index == this.tokens.Count
                    ? Token.Eof
                    : this.tokens[index];
            }

            private void Eat(Token token)
            {
                var next = this.tokens[this.index];
                if (next != token) { throw new InvalidOperationException($"Expected {token.Name}, got {next.Name}"); }
                this.listener.OnSymbolParsed(next, rule: null);
                ++this.index;
            }
        }
    }
}
