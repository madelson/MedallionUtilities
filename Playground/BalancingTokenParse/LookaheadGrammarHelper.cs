using Medallion.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    sealed class LookaheadGrammarHelper
    {
        private readonly IReadOnlyList<Rule> rules;
        private readonly ILookup<NonTerminal, Rule> rulesByProduced;
        private readonly FirstFollowCalculator firstFollow;

        private readonly Dictionary<NonTerminal, IReadOnlyList<Rule>> addedSymbols = new Dictionary<NonTerminal, IReadOnlyList<Rule>>();

        public LookaheadGrammarHelper(IEnumerable<Rule> rules)
        {
            this.rules = rules.ToArray();
            this.rulesByProduced = this.rules.ToLookup(r => r.Produced);
            this.firstFollow = new FirstFollowCalculator(this.rules);
        }

        public void Run()
        {
            var table = this.firstFollow.NonTerminals.ToDictionary(nt => nt, _ => new Dictionary<Token, List<Rule>>());
            foreach (var rule in this.rules)
            {
                var subTable = table[rule.Produced];
                foreach (var token in this.firstFollow.NextOf(rule))
                {
                    List<Rule> existing;
                    if (subTable.TryGetValue(token, out existing)) { existing.Add(rule); }
                    else { subTable.Add(token, new List<Rule> { rule }); }
                }
            }

            var conflicts = table.SelectMany(kvp => kvp.Value)
                .Where(kvp => kvp.Value.Count > 1)
                .ToList();
            var resolvedConflicts = new List<Tuple<NonTerminal, Token, Action>>();

            for (var i = 0; i < conflicts.Count; ++i)
            {

            }
        }

        private Tuple<Action, IReadOnlyList<Rule>> ResolveConflict(Token token, IReadOnlyList<Rule> rules)
        {
            if (rules.Count == 0) { throw new ArgumentException(nameof(rules)); } // sanity check

            if (rules.Any(r => r.Symbols.Count == 0))
            {
                // in the future, we can deal with this by looking at the follow
                throw new NotSupportedException("Cannot differentiate empty rules");
            }

            // if we have a common prefix symbol, parse that and then differentiate the remainder
            var prefixLength = 0;
            while (rules.All(r => r.Symbols.Count > prefixLength)
                && rules.Select(r => r.Symbols[prefixLength]).Distinct().Count() == 1)
            {
                ++prefixLength;
            }
            if (prefixLength > 0)
            {
                // todo
            }

            // first, see if we have any new symbol != rules.Produced whose rules have no empty productions AND which form a prefix
            // if so, substitute that in
            var prefixMapping = this.addedSymbols.Select(
                    kvp => kvp.Key == rules[0].Produced ? null
                        : kvp.Value.Any(r => r.Symbols.Count == 0) ? null
                        : GetOriginalToPrefixMapping(originals: rules, prefixes: kvp.Value)
                )
                .FirstOrDefault(m => m != null);
            if (prefixMapping != null)
            {
                // todo
            }

            throw new NotImplementedException();
        }

        private static IReadOnlyDictionary<Rule, Rule> GetOriginalToPrefixMapping(IReadOnlyList<Rule> originals, IReadOnlyList<Rule> prefixes)
        {
            // for now, we are insisting that each original get the longest matching prefix, and that each prefix be the longest match for
            // at least one original

            var originalsToPrefixes = originals.ToDictionary(
                o => o,
                o => prefixes.Where(p => p.Symbols.Count <= o.Symbols.Count && o.Symbols.Take(p.Symbols.Count).SequenceEqual(p.Symbols))
                    .OrderByDescending(p => p.Symbols.Count)
                    .FirstOrDefault()
            );

            if (originalsToPrefixes.ContainsValue(null)
                || prefixes.Except(originalsToPrefixes.Values).Any())
            {
                return null; // bad match
            }

            return originalsToPrefixes;
        }

        private sealed class Action
        {
            /// <summary>
            /// We know exactly the rule to be parsed
            /// </summary>
            public Rule Rule { get; set; }
            
            public IReadOnlyList<Symbol> ToParse { get; set; }
            public NonTerminal DiscriminatorSymbol { get; set; }
        }

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
