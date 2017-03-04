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
            var table = this.firstFollow.NonTerminals.ToDictionary(nt => nt, _ => new Dictionary<Token, List<Rule>>());
            foreach (var rule in this.rules)
            {
                var subTable = table[rule.Produced];
                foreach (var token in this.NextOf(rule))
                {
                    List<Rule> existing;
                    if (subTable.TryGetValue(token, out existing)) { existing.Add(rule); }
                    else { subTable.Add(token, new List<Rule> { rule }); }
                }
            }

            var finalTable = table.ToDictionary(kvp => kvp.Key, kvp => new Dictionary<Token, Action>());
            foreach (var nonTerminal in table.Keys)
            {
                foreach (var kvp in table[nonTerminal])
                {
                    finalTable[nonTerminal].Add(
                        kvp.Key, 
                        kvp.Value.Count > 1 
                            ? new Action { Discriminator = this.GetDiscriminator(kvp.Key, kvp.Value) }
                            : new Action { Rule = kvp.Value[0] }
                    );
                }
            }

            return new TableParser { StartSymbol = this.firstFollow.StartSymbol, Table = finalTable };
        }
        
        private DiscriminatorInfo GetDiscriminator(Token token, IReadOnlyList<Rule> rules)
        {
            var discriminators = rules.Select(r => new { r, remainings = this.GetRemainings(r, token) })
                .ToArray();

            var discriminatorSymbol = new NonTerminal($"DISC-ON-{token}-{Guid.NewGuid()}");
            var discriminatorRules = discriminators.SelectMany(t => t.remainings, (t, remaining) => new { origRule = t.r, newRule = new Rule(discriminatorSymbol, remaining) })
                .Select(t => new { t.origRule, t.newRule, next = this.NextOf(new Rule(t.origRule.Produced, t.newRule.Symbols)) })
                .ToArray();

            var allNexts = discriminatorRules.SelectMany(t => t.next).Distinct().ToArray();
            var result = new DiscriminatorInfo { Symbol = discriminatorSymbol };
            foreach (var next in allNexts)
            {
                var matchingRules = discriminatorRules.Where(t => t.next.Contains(next)).ToArray();
                if (matchingRules.Length == 1)
                {
                    result.SimpleDiscriminations.Add(next, matchingRules.Single().origRule);
                }
                else
                {
                    var subDiscriminator = this.GetDiscriminator(next, matchingRules.Select(t => t.newRule).ToArray());
                    result.ComplexDiscriminations.Add(
                        next,
                        Tuple.Create(
                            matchingRules.ToDictionary(t => t.newRule, t => t.origRule),
                            subDiscriminator
                        )
                    );
                }
            }

            return result;
        }

        private List<List<Symbol>> GetRemainings(Rule rule, Token toRemove)
        {
            var result = new List<List<Symbol>>();
            this.GetRemainings(new[] { rule }, toRemove, result);
            return result;
        }

        private void GetRemainings(NonTerminal nonTerminal, Token toRemove, List<List<Symbol>> result)
        {
            var matches = this.rules.Where(r => r.Produced == nonTerminal && this.NextOf(r).Contains(toRemove));
            this.GetRemainings(matches, toRemove, result);
        }

        private void GetRemainings(IEnumerable<Rule> rules, Token toRemove, List<List<Symbol>> result)
        {
            foreach (var rule in rules)
            {
                if (rule.Symbols.Count == 0) { throw new InvalidOperationException("Can't differentiate: reached empty rule!"); }

                if (rule.Symbols[0] == toRemove)
                {
                    result.Add(rule.Symbols.Skip(1).ToList());
                    return;
                }

                for (var i = 0; i < rule.Symbols.Count; ++i)
                {
                    var skipCount = result.Count; // don't update the results we have already
                    this.GetRemainings((NonTerminal)rule.Symbols[i], toRemove, result);

                    for (var j = skipCount; j < result.Count; ++j)
                    {
                        // add the suffix
                        result[j].AddRange(rule.Symbols.Skip(i + 1));
                    }

                    // if the current symbol is non-nullable or the remainder of the rule can't start with
                    // toRemove, we're done
                    if (!this.firstFollow.First[rule.Symbols[0]].Contains(null)
                        || !this.NextOf(rule, skipCount: i + 1).Contains(toRemove))
                    {
                        break;
                    }
                } 
            }
        }

        private ISet<Token> NextOf(Rule rule, int skipCount = 0)
        {
            var result = new HashSet<Token>();
            for (var i = skipCount; i < rule.Symbols.Count; ++i)
            {
                var firsts = this.firstFollow.First[rule.Symbols[i]];
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

        private class Action
        {
            public DiscriminatorInfo Discriminator { get; set; }
            public Rule Rule { get; set; }
        }

        private class DiscriminatorInfo
        {
            public NonTerminal Symbol { get; set; }
            public Dictionary<Token, Rule> SimpleDiscriminations { get; } = new Dictionary<Token, Rule>();
            /// <summary>
            /// Maps the next seen token to:
            /// 1. a mapping from pseudo-rule to original grammar rule
            /// 2. a <see cref="DiscriminatorInfo"/> which picks between pseudo rules
            /// </summary>
            public Dictionary<Token, Tuple<Dictionary<Rule, Rule>, DiscriminatorInfo>> ComplexDiscriminations { get; } = new Dictionary<Token, Tuple<Dictionary<Rule, Rule>, DiscriminatorInfo>>();
        }

        private class TableParser : IParser
        {
            public NonTerminal StartSymbol { get; set; }
            public IReadOnlyDictionary<NonTerminal, Dictionary<Token, Action>> Table { get; set; }

            private IReadOnlyList<Token> tokens;
            private IParserListener listener;
            private int index;
            private int discriminatorIndex;

            public void Parse(IReadOnlyList<Token> tokens, IParserListener listener)
            {
                this.tokens = tokens;
                this.listener = listener;
                this.index = 0;

                this.Parse(this.StartSymbol);
            }

            private void Parse(NonTerminal symbol)
            {
                Action action;
                if (!this.Table[symbol].TryGetValue(this.Peek(), out action))
                {
                    throw new InvalidOperationException($"Could not determine a parse rule to apply for {symbol.Name} on seeing {this.Peek().Name}");
                }

                Rule rule;
                if (action.Rule != null) { rule = action.Rule; }
                else
                {
                    this.discriminatorIndex = this.index;
                    rule = this.Select(action.Discriminator);
                }

                foreach (var childSymbol in rule.Symbols)
                {
                    if (childSymbol is Token) { this.Eat((Token)childSymbol); }
                    else { this.Parse((NonTerminal)childSymbol); }
                }

                this.listener.OnSymbolParsed(symbol, rule);
            }

            private Rule Select(DiscriminatorInfo discriminator)
            {
                ++this.discriminatorIndex; // eats the current token
                var nextToken = this.DiscriminatorPeek();

                if (discriminator.SimpleDiscriminations.ContainsKey(nextToken))
                {
                    return discriminator.SimpleDiscriminations[nextToken];
                }
                
                if (discriminator.ComplexDiscriminations.ContainsKey(nextToken))
                {
                    var nestedDiscriminationInfo = discriminator.ComplexDiscriminations[nextToken];
                    return nestedDiscriminationInfo.Item1[this.Select(nestedDiscriminationInfo.Item2)];
                }

                throw new InvalidOperationException($"Expected one of the following tokens ({string.Join(", ", discriminator.SimpleDiscriminations.Keys.Concat(discriminator.ComplexDiscriminations.Keys))}), but found {nextToken}");
            }

            private Token DiscriminatorPeek()
            {
                return this.discriminatorIndex == this.tokens.Count
                    ? Token.Eof
                    : this.tokens[this.discriminatorIndex];
            }

            private Token Peek()
            {
                return this.index == this.tokens.Count
                    ? Token.Eof
                    : this.tokens[this.index];
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
