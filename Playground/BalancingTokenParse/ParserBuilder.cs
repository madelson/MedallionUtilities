using Medallion.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    internal class ParserBuilder
    {
        private readonly FirstFollowCalculator baseFirstFollow;
        private readonly InternalFirstFollowProvider firstFollow;
        private readonly Dictionary<NonTerminal, IReadOnlyList<Rule>> rules;
        private readonly Queue<NonTerminal> remainingSymbols;
        private readonly HashSet<NonTerminal> discriminatorSymbols = new HashSet<NonTerminal>();
        private readonly Dictionary<NonTerminal, IParserNode> result = new Dictionary<NonTerminal, IParserNode>();

        private ParserBuilder(IEnumerable<Rule> rules)
        {
            this.rules = rules.GroupBy(r => r.Produced)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<Rule>)g.ToArray());
            this.baseFirstFollow = new FirstFollowCalculator(this.rules.SelectMany(kvp => kvp.Value).ToArray());
            this.firstFollow = new InternalFirstFollowProvider(this.baseFirstFollow);
            this.remainingSymbols = new Queue<NonTerminal>(this.baseFirstFollow.NonTerminals);
        }

        public static Dictionary<NonTerminal, IParserNode> CreateParser(IEnumerable<Rule> rules)
        {
            return new ParserBuilder(rules).CreateParser();
        }

        private Dictionary<NonTerminal, IParserNode> CreateParser()
        {
            while (this.remainingSymbols.Count > 0)
            {
                var next = this.remainingSymbols.Dequeue();
                this.CreateParserNode(next);
            }

            return result;
        }

        private IParserNode CreateParserNode(NonTerminal symbol)
        {
            // don't recompute if we already have; this is important for making sure we don't stack-overflow
            IParserNode existing;
            if (this.result.TryGetValue(symbol, out existing)) { return existing; }

            var symbolParser = this.CreateParserNode(this.rules[symbol].Select(r => new PartialRule(r)).ToArray());
            this.result.Add(symbol, symbolParser);
            return symbolParser;
        }

        private IParserNode CreateParserNode(IReadOnlyList<PartialRule> rules)
        {
            // if we only have one rule, we just parse that
            if (rules.Count == 1)
            {
                return new ParseRuleNode(rules.Single());
            }

            // next, see what we can do with LL(1) single-token lookahead
            var tokenLookaheadTable = rules.SelectMany(r => this.firstFollow.NextOf(r), (r, t) => new { rule = r, token = t })
                .GroupBy(t => t.token, t => t.rule)
                .ToDictionary(g => g.Key, g => g.ToArray());

            // if there is only one entry in the table, just create a non-LL(1) node for that entry
            // we know that this must be non-LL(1) because we already checked for the single-rule case above
            if (tokenLookaheadTable.Count == 1)
            {
                return this.CreateNonLL1ParserNode(tokenLookaheadTable.Single().Key, tokenLookaheadTable.Single().Value);
            }

            // else, create a token lookahead node mapping from the table
            return new TokenLookaheadNode(
                tokenLookaheadTable.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Length == 1
                        ? new ParseRuleNode(kvp.Value.Single())
                        : this.CreateNonLL1ParserNode(kvp.Key, kvp.Value)
                )
            );
        }
        
        private IParserNode CreateNonLL1ParserNode(Token lookaheadToken, IReadOnlyList<PartialRule> rules)
        {
            // sanity checks
            if (rules.Count <= 1) { throw new ArgumentException(nameof(rules), "must be more than one"); }
            if (rules.Select(r => r.Produced).Distinct().Count() != 1) { throw new ArgumentException(nameof(rules), "must all produce the same symbol"); }

            // look for a common prefix containing non-terminals (we reserve token-only prefixes for transformation into discriminators)

            var prefixLength = Enumerable.Range(0, count: rules.Min(r => r.Symbols.Count))
                .TakeWhile(i => rules.Skip(1).All(r => r.Symbols[i] == rules[0].Symbols[i]))
                .Select(i => i + 1)
                .LastOrDefault();

            if (prefixLength > 0 && !rules[0].Symbols.Take(prefixLength).All(r => r is Token))
            {
                return new ParsePrefixSymbolsNode(
                    rules[0].Symbols.Take(prefixLength),
                    this.CreateParserNode(rules.Select(r => new PartialRule(r, start: prefixLength)).ToArray())
                );
            }

            // next, if we are producing a discriminator, see if an existing discriminator is a prefix. This 
            // lets us handle recursion within the lookahead grammar
            if (this.discriminatorSymbols.Contains(rules[0].Produced))
            {
                var match = this.discriminatorSymbols
                    .Where(s => s != rules[0].Produced)
                    .Select(s => new { discriminator = s, mapping = this.MapSymbolRules(s, rules) })
                    .FirstOrDefault(t => t.mapping != null);
                if (match != null)
                {
                    // first parse the discriminator
                    var discriminatorParse = this.CreateParserNode(match.discriminator);
                    // then map it's result to determine how to parse the remaining symbols
                    var mapResultNode = new MapResultNode(
                        discriminatorParse,
                        match.mapping.GroupBy(kvp => kvp.Value, kvp => new PartialRule(kvp.Key, start: kvp.Value.Symbols.Count))
                            .ToDictionary(
                                g => g.Key,
                                g => this.CreateParserNode(g.ToArray())
                            )
                    );
                    return mapResultNode;
                }
            }

            // otherwise, we will need to create a new node as part of the lookahead grammar
            return this.CreateGrammarLookaheadParserNode(lookaheadToken, rules);
        }
        
        private IParserNode CreateGrammarLookaheadParserNode(Token lookaheadToken, IReadOnlyList<PartialRule> rules)
        {
            // sanity checks
            if (rules.Select(r => r.Rule).Distinct().Count() != rules.Count) { throw new ArgumentException(nameof(rules), "must be partials of distinct rules"); }

            var suffixToRuleMapping = rules.SelectMany(r => this.GatherPostTokenSuffixes(lookaheadToken, r), (r, suffix) => new { r, suffix })
                // note: this will throw if two rules have the same suffix, but it's not very elegant
                .ToDictionary(t => t.suffix, t => t.r, (IEqualityComparer<IReadOnlyList<Symbol>>)EqualityComparers.GetSequenceComparer<Symbol>());

            //var match = this.discriminatorSymbols
            //    .Select(s => new { discriminator = s, mapping = this.MapSymbolRules(s, suffixToRuleMapping.Keys) })
            //    .FirstOrDefault(t => t.mapping != null);
            
            //if (match != null && match.mapping.All(kvp => kvp.Key.Count == kvp.Value.Symbols.Count))
            //{
            //    // exact match: don't create a new discriminator
            //    return new GrammarLookaheadNode(
            //        lookaheadToken,
            //        match.discriminator,
            //        match.mapping.ToDictionary(
            //            kvp => kvp.Value,
            //            kvp => (IParserNode)new ParseRuleNode(suffixToRuleMapping[kvp.Key])
            //        )
            //    );
            //}

            // create the discriminator symbol
            var discriminator = new NonTerminal("T" + this.discriminatorSymbols.Count);
            this.discriminatorSymbols.Add(discriminator);
            this.rules[discriminator] = suffixToRuleMapping.Keys.Select(symbols => new Rule(discriminator, symbols)).ToArray();
            this.firstFollow.Register(discriminator, suffixToRuleMapping.Keys);
            this.remainingSymbols.Enqueue(discriminator);
            
            return new GrammarLookaheadNode(
                lookaheadToken,
                discriminator,
                // map each discriminator rule back to the corresponding original rule
                this.rules[discriminator].ToDictionary(
                    r => r,
                    r => suffixToRuleMapping[r.Symbols].Rule
                )
            );
        }

        private Dictionary<PartialRule, Rule> MapSymbolRules(NonTerminal discriminator, IReadOnlyCollection<PartialRule> toMap)
        {
            var result = new Dictionary<PartialRule, Rule>();
            foreach (var rule in this.rules[discriminator])
            {
                if (rule.Symbols.Count == 0) { return null; }

                foreach (var match in toMap.Where(r => r.Symbols.Take(rule.Symbols.Count).SequenceEqual(rule.Symbols)))
                {
                    Rule existingMapping;
                    if (!result.TryGetValue(match, out existingMapping) || existingMapping.Symbols.Count < rule.Symbols.Count)
                    {
                        result[match] = rule;
                    }
                }
            }

            // require that all rules and all partial rules were mapped
            if (result.Count != toMap.Count) { return null; }
            if (this.rules[discriminator].Except(result.Values).Any()) { return null; }

            return result;
        }

        // to support initial prefix, we'll just add the ability to pass a null rule plus a suffix stack
        private ISet<IReadOnlyList<Symbol>> GatherPostTokenSuffixes(Token prefixToken, PartialRule rule)
        {
            var result = new HashSet<IReadOnlyList<Symbol>>();
            this.GatherPostTokenSuffixes(prefixToken, rule, ImmutableStack<Symbol>.Empty, result);
            return result;
        }

        /// <summary>
        /// Recursively gathers a set of <see cref="Symbol"/> lists which could form the remainder after consuming
        /// a <paramref name="prefixToken"/>
        /// </summary>
        private void GatherPostTokenSuffixes(
            Token prefixToken,
            PartialRule rule,
            ImmutableStack<Symbol> suffix,
            ISet<IReadOnlyList<Symbol>> result)
        {
            if (rule.Symbols.Count == 0)
            {
                if (!suffix.IsEmpty)
                {
                    var nextSuffixSymbol = suffix.Peek();
                    if (nextSuffixSymbol is Token)
                    {
                        if (nextSuffixSymbol == prefixToken)
                        {
                            result.Add(suffix.Skip(1).ToArray());
                        }
                    }
                    else
                    {
                        var newSuffix = suffix.Pop();
                        var innerRules = this.rules[(NonTerminal)nextSuffixSymbol]
                            .Where(r => this.firstFollow.NextOf(r).Contains(prefixToken));
                        foreach (var innerRule in innerRules)
                        {
                            this.GatherPostTokenSuffixes(prefixToken, new PartialRule(innerRule), newSuffix, result);
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException("can't remove prefix from empty");
                } 
            }
            else if (rule.Symbols[0] is Token)
            {
                if (rule.Symbols[0] == prefixToken)
                {
                    result.Add(rule.Symbols.Skip(1).Concat(suffix).ToArray());
                }

                return;
            }
            else
            {
                // the new suffix adds the rest of the current rule, from back to front
                // to preserve ordering
                var newSuffix = suffix;
                for (var i = rule.Symbols.Count - 1; i > 0; --i)
                {
                    newSuffix = newSuffix.Push(rule.Symbols[i]);
                }

                var innerRules = this.rules[(NonTerminal)rule.Symbols[0]]
                    .Where(r => this.firstFollow.NextOf(r).Contains(prefixToken));
                foreach (var innerRule in innerRules)
                {
                    this.GatherPostTokenSuffixes(prefixToken, new PartialRule(innerRule), newSuffix, result);
                }
            }
        }

        private string DebugGrammar => string.Join(
            Environment.NewLine + Environment.NewLine,
            this.rules.Select(kvp => $"{kvp.Key}:{Environment.NewLine}" + string.Join(Environment.NewLine, kvp.Value.Select(r => "\t" + r)))
        );

        private class InternalFirstFollowProvider : IFirstFollowProvider
        {
            private readonly IFirstFollowProvider originalGrammarProvider;

            private readonly Dictionary<Symbol, IImmutableSet<Token>> additionalFirsts = new Dictionary<Symbol, IImmutableSet<Token>>();
            private readonly Dictionary<Symbol, IImmutableSet<Token>> additionalFollows = new Dictionary<Symbol, IImmutableSet<Token>>();

            public InternalFirstFollowProvider(IFirstFollowProvider provider)
            {
                this.originalGrammarProvider = provider;
            }

            public void Register(NonTerminal symbol, IEnumerable<IReadOnlyList<Symbol>> productions)
            {
                var firstsBuilder = ImmutableHashSet.CreateBuilder<Token>();
                var followsBuilder = ImmutableHashSet.CreateBuilder<Token>();
                foreach (var symbols in productions)
                {
                    firstsBuilder.UnionWith(this.FirstOf(symbols));
                    // todo what if symbols is empty... we could use the leading symbol?
                    // note that this is over-broad; we could leverage context instead. I like this more
                    // constrained approach because it lets us re-use the symbol in other contexts
                    followsBuilder.UnionWith(this.FollowOf(symbols[symbols.Count - 1]));
                }

                this.additionalFirsts.Add(symbol, firstsBuilder.ToImmutable());
                this.additionalFollows.Add(symbol, followsBuilder.ToImmutable());
            }

            public IImmutableSet<Token> FirstOf(Symbol symbol)
            {
                IImmutableSet<Token> additional;
                return this.additionalFirsts.TryGetValue(symbol, out additional)
                    ? additional
                    : this.originalGrammarProvider.FirstOf(symbol);
            }

            public IImmutableSet<Token> FollowOf(Symbol symbol)
            {
                IImmutableSet<Token> additional;
                return this.additionalFollows.TryGetValue(symbol, out additional)
                    ? additional
                    : this.originalGrammarProvider.FollowOf(symbol);
            }
        }
    }
}
