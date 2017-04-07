using Medallion.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    // for right recursive T + E
    // for left recursive T (+ T)*, but only fire the listener on every parse of (+ T)

    class LeftRecursionRewriter
    {
        private readonly ImmutableDictionary<NonTerminal, ImmutableList<Rule>> rulesByProduced;
        private readonly ImmutableDictionary<NonTerminal, NonTerminal> aliases;
        private readonly ImmutableDictionary<Rule, Rule> ruleMapping;
        private readonly ImmutableHashSet<Rule> rightAssociativeRules;
        private readonly FirstFollowCalculator firstFollow;

        private LeftRecursionRewriter(
            ImmutableDictionary<NonTerminal, ImmutableList<Rule>> rulesByProduced,
            ImmutableDictionary<Rule, Rule> ruleMapping,
            ImmutableDictionary<NonTerminal, NonTerminal> aliases,
            ImmutableHashSet<Rule> rightAssociativeRules)
        {
            this.rulesByProduced = rulesByProduced;
            this.aliases = aliases;
            this.ruleMapping = ruleMapping;
            this.rightAssociativeRules = rightAssociativeRules;
            this.firstFollow = new FirstFollowCalculator(ruleMapping.Keys.ToArray());

            var missings = this.rulesByProduced.SelectMany(kvp => kvp.Value)
                .Where(r => !this.ruleMapping.ContainsKey(r))
                .ToList();
            if (missings.Any())
            {
                throw new ArgumentException($"Unmapped: {string.Join(", ", missings)}");
            }
        }

        public static IReadOnlyDictionary<Rule, Rule> Rewrite(IEnumerable<Rule> grammar, ImmutableHashSet<Rule> rightAssociativeRules)
        {
            var rulesByProduced = ImmutableDictionary.CreateRange(
                grammar.GroupBy(r => r.Produced)
                    .Select(g => new KeyValuePair<NonTerminal, ImmutableList<Rule>>(g.Key, ImmutableList.CreateRange(g)))
            );
            var aliases = ImmutableDictionary.CreateRange(
                rulesByProduced.Keys.Select(s => new { symbol = s, aliasOf = GetAliasedSymbol(s, rulesByProduced) })
                    .Where(t => t.aliasOf != null)
                    .Select(t => new KeyValuePair<NonTerminal, NonTerminal>(t.symbol, t.aliasOf))
            );
            var ruleMapping = ImmutableDictionary.CreateRange(grammar.Select(r => new KeyValuePair<Rule, Rule>(
                r, 
                r.Symbols.Count == 1 && r.Symbols[0] is NonTerminal && aliases.ContainsKey((NonTerminal)r.Symbols[0]) ? null : r
            )));

            var rewriter = new LeftRecursionRewriter(rulesByProduced, ruleMapping, aliases, rightAssociativeRules);
            rewriter.CheckForProblematicLeftRecursion();
            rewriter = rewriter.InlineAliases();

            while (true)
            {
                var rewritten = rewriter.RewriteOne();
                if (rewritten == rewriter) { break; }
                rewriter = rewritten;
            }

            return rewriter.ruleMapping;
        }

        private bool IsAliasOf(Symbol a, NonTerminal b) =>
            Traverse.Along(a as NonTerminal, s => this.aliases.GetValueOrDefault(s)).Contains(b);

        private static NonTerminal GetAliasedSymbol(NonTerminal symbol, IReadOnlyDictionary<NonTerminal, ImmutableList<Rule>> rules)
        {
            var referencingRules = rules.SelectMany(kvp => kvp.Value)
                .Where(r => r.Symbols.Contains(symbol))
                .ToArray();
            return referencingRules.Length == 1 && referencingRules.Single().Symbols.Count == 1
                ? referencingRules.Single().Produced
                : null;
        }

        #region ---- Recursion rewrite ----
        private LeftRecursionRewriter RewriteOne()
        {
            if (this.aliases.Count != 0) { throw new InvalidOperationException("sanity check"); }

            // Example with all right associative (left-associative just replaces (+ E)? with (+ EX)*)
            // 
            // START
            // E = -E | E * E | E + E | ID
            //
            // ONE REWRITE
            // E = E0 (* E)? | E + E
            // E0 = -E0 | ID
            //
            // TWO REWRITES
            // E = E1 (+ E)?
            // E1 = E0 (* E)?
            // E0 = -E0 | ID

            // pick the highest precedence left-recursive rule we can find
            var leftRecursiveRule = this.rulesByProduced.SelectMany(kvp => kvp.Value)
                .FirstOrDefault(IsSimpleLeftRecursive);
            if (leftRecursiveRule == null)
            {
                return this;
            }

            var isBinary = IsSimpleRightRecursive(leftRecursiveRule);

            var replacements = new List<RuleReplacement>();

            var newSymbol = new NonTerminal($"{leftRecursiveRule.Produced}_{this.rulesByProduced.Count(kvp => kvp.Key.Name.StartsWith(leftRecursiveRule.Produced.Name + "_"))}");

            // determine which rules get pushed to the new symbol. These will be any rules that are neither left or simple right recursive
            // as well as any simple right-recursive rules that are higher precedence than the current rule. Note: we don't use ToDictionary
            // here because we want to preserve order
            var leftRecursiveRuleIndex = this.rulesByProduced[leftRecursiveRule.Produced].IndexOf(leftRecursiveRule);
            replacements.AddRange(
                this.rulesByProduced[leftRecursiveRule.Produced]
                    .Select(
                        // this check is simpler than the requirement stated above, because we leverage the fact that we know the current
                        // rule is the highest-precedence left-recursive rule for the symbol
                        (r, index) => index < leftRecursiveRuleIndex || (!IsSimpleLeftRecursive(r) && !IsSimpleRightRecursive(r))
                            ? new RuleReplacement(r, new Rule(newSymbol, r.Symbols))
                            : null
                    )
                    .Where(t => t != null)
            );
            
            // associativity is only a binary concept
            var isLeftAssociative = isBinary && !this.rightAssociativeRules.Contains(leftRecursiveRule);

            IReadOnlyDictionary<Rule, Rule> mappedExistingSymbolRules;
            if (!isLeftAssociative)
            {
                // for E ? E : E, we have E = T | T ? E : E
                // the former is a no-op
                replacements.AddRange(
                    new[]
                    {
                        new RuleReplacement(null, new Rule(leftRecursiveRule.Produced, newSymbol)),
                        new RuleReplacement(leftRecursiveRule, new Rule(leftRecursiveRule.Produced, leftRecursiveRule.Symbols.Select((s, i) => i == 0 ? newSymbol : s))),
                    }   
                );
            }
            else
            {
                // for left associativity, we do:
                // E = E + E
                // E = T List<+T>
                // every parse of "+ T" maps to E + E rule
                var suffixSymbol = new NonTerminal($"'{string.Join(" ", leftRecursiveRule.Symbols.Skip(1))}'");
                var suffixListSymbol = new NonTerminal($"List<{suffixSymbol}>");

                replacements.AddRange(
                    new[] 
                    {
                        // E = T List<+E>
                        new RuleReplacement(null, new Rule(leftRecursiveRule.Produced, newSymbol, suffixListSymbol)),

                        // List<+E> = +E List<+E> | empty
                        new RuleReplacement(null, new Rule(suffixListSymbol, suffixSymbol, suffixListSymbol)),
                        new RuleReplacement(null, new Rule(suffixListSymbol)),

                        // +E = + T
                        new RuleReplacement(leftRecursiveRule, new Rule(suffixSymbol, leftRecursiveRule.Symbols.Select((s, i) => i == leftRecursiveRule.Symbols.Count - 1 ? newSymbol : s).Skip(1)))
                    }
                );
            }

            return this.Replace(replacements);
        }

        private LeftRecursionRewriter Replace(IReadOnlyList<RuleReplacement> replacements)
        {
            var rulesByProducedBuilder = this.rulesByProduced.ToBuilder();
            var ruleMappingBuilder = this.ruleMapping.ToBuilder();
            var rightAssociativeRulesBuilder = this.rightAssociativeRules.ToBuilder();

            var newSymbols = new HashSet<NonTerminal>(replacements.Select(r => r.NewRule.Produced).Where(s => !this.rulesByProduced.ContainsKey(s)));
            rulesByProducedBuilder.AddRange(newSymbols.Select(s => new KeyValuePair<NonTerminal, ImmutableList<Rule>>(s, ImmutableList<Rule>.Empty)));

            foreach (var replacement in replacements)
            {
                if (replacement.OrigRule != null)
                {
                    rulesByProducedBuilder[replacement.OrigRule.Produced] = 
                        rulesByProducedBuilder[replacement.OrigRule.Produced].Remove(replacement.OrigRule);
                    ruleMappingBuilder.Remove(replacement.OrigRule);
                    ruleMappingBuilder.Add(replacement.NewRule, this.ruleMapping[replacement.OrigRule]);

                    // this logic is tricky. Basically we want to preserve the right-associativity mapping as we translate
                    // rules. However, for a replaced rule like E ? E : E => T ? E : E, we are no longer left-recursive so
                    // we don't want to add it. The produced check wouldn't be sufficient for left-recursive rules, but that's
                    // ok since this only happens for right-associative anyway. Another way we could do this would be to
                    // leave the right-associative collection alone across this and alias rewrites and index into it using
                    // ruleMapping
                    if (rightAssociativeRulesBuilder.Remove(replacement.OrigRule)
                        && replacement.OrigRule.Produced != replacement.NewRule.Produced)
                    {
                        rightAssociativeRulesBuilder.Add(replacement.NewRule);
                    }
                }
                else
                {
                    ruleMappingBuilder.Add(replacement.NewRule, null);
                }
            }

            // add to rulesByProduced in batch to get the ordering right. All replacements are in precedence order, but
            // when added to existing lists they are all higher-priority. Thus we will do InsertRange for each batch
            var newRulesByProduced = replacements.Select(r => r.NewRule).GroupBy(r => r.Produced);
            foreach (var producedGroup in newRulesByProduced)
            {
                rulesByProducedBuilder[producedGroup.Key] = rulesByProducedBuilder[producedGroup.Key].InsertRange(0, producedGroup);
            }

            return new LeftRecursionRewriter(
                rulesByProducedBuilder.ToImmutable(),
                ruleMappingBuilder.ToImmutable(),
                this.aliases,
                rightAssociativeRulesBuilder.ToImmutable()
            );
        }

        private static bool IsSimpleLeftRecursive(Rule rule) => rule.Symbols.Count > 0 && rule.Symbols[0] == rule.Produced;

        private static bool IsSimpleRightRecursive(Rule rule) => rule.Symbols.Count > 0 && rule.Symbols[rule.Symbols.Count - 1] == rule.Produced;

        private sealed class RuleReplacement : Tuple<Rule, Rule>
        {
            public RuleReplacement(Rule origRule, Rule newRule) 
                : base(origRule, newRule)
            {
                if (newRule == null) { throw new ArgumentNullException(nameof(newRule)); }
            }

            public Rule OrigRule => this.Item1;
            public Rule NewRule => this.Item2;
        }
        #endregion

        // for each produced symbol, run process
        // start with highest-priority rule; push all non-left-recursive or higher-priority right-recursive rules into the new symbol
        // when done, do a check for full LR and throw if we find it (could also do this upfront to get better errors)

        #region ---- Problematic left recursion check ----
        private void CheckForProblematicLeftRecursion()
        {
            var problem = this.ruleMapping.Keys
                .Select(r => this.CheckForProblematicLeftRecursion(ImmutableList.Create(r), ImmutableHashSet<Rule>.Empty, RecursionProblem.None))
                .FirstOrDefault(t => t != null);

            if (problem != null)
            {
                throw new InvalidOperationException($"Found {problem.Item2} left recursion for {problem.Item1[0].Produced}: {string.Join(" => ", problem.Item1)}");
            }
        }

        private Tuple<ImmutableList<Rule>, RecursionProblem> CheckForProblematicLeftRecursion(
            ImmutableList<Rule> context,
            IImmutableSet<Rule> visited,
            RecursionProblem problem)
        {
            var rule = context[context.Count - 1];
            // avoid infinite recursion
            if (visited.Contains(rule)) { return null; }

            var produced = context[0].Produced;
            var newVisited = visited.Add(rule);
            var currentProblem = problem;
            for (var i = 0; i < rule.Symbols.Count; ++i)
            {
                var symbol = rule.Symbols[i] as NonTerminal;
                if (symbol == null) { break; }

                // found left recursion
                if (symbol == produced)
                {
                    if (currentProblem != RecursionProblem.None)
                    {
                        return Tuple.Create(context, currentProblem);
                    }

                    // otherwise, we don't need to recurse but we still need to check hidden
                }
                else
                {
                    // otherwise, recurse on each rule
                    var isAlias = this.IsAliasOf(symbol, produced);
                    foreach (var symbolRule in this.rulesByProduced[symbol])
                    {
                        var result = this.CheckForProblematicLeftRecursion(
                            context.Add(symbolRule),
                            newVisited,
                            currentProblem | (isAlias ? RecursionProblem.None : RecursionProblem.Indirect)
                        );
                        if (result != null) { return result; }
                    }
                }

                // continue past the symbol if it's nullable. Once we do this we're in the realm of hidden left recursion
                if (!this.firstFollow.IsNullable(symbol)) { break; }
                currentProblem |= RecursionProblem.Hidden;
            }

            // if we reach here without returning, we found nothing
            return null;
        }

        [Flags]
        private enum RecursionProblem
        {
            None = 0,
            Hidden = 1,
            Indirect = 2,
        }
        #endregion

        #region ---- Alias rewrite ----
        /// <summary>
        /// Inlines all aliased rules into the rule set of the aliased symbol, preserving the original
        /// alias name via <see cref="ruleMapping"/>
        /// </summary>
        private LeftRecursionRewriter InlineAliases()
        {
            if (this.aliases.Count == 0) { return this; }

            // if a is an alias of b, we will inline a before b so that a gets inlined 
            // into b before b gets inlined into something
            var comparer = Comparer<NonTerminal>.Create(
                (a, b) => this.IsAliasOf(a, b) ? -1
                    : this.IsAliasOf(b, a) ? 1
                    // unrelated aliases are considered equal, ensuring transitivity
                    : 0
            );
            var orderedAliasSymbols = this.aliases.OrderBy(s => s.Key, comparer);

            var rulesByProducedBuilder = this.rulesByProduced.ToBuilder();
            var ruleMappingBuilder = this.ruleMapping.ToBuilder();
            var rightAssociativeRulesBuilder = this.rightAssociativeRules.ToBuilder();
            foreach (var kvp in orderedAliasSymbols)
            {
                var aliasOfRulesBuilder = rulesByProducedBuilder[kvp.Value].ToBuilder();
                var ruleMapping = rulesByProducedBuilder[kvp.Key].Select(
                        r => new { origRule = r, newRule = new Rule(kvp.Value, r.Symbols), mappedRule = ruleMappingBuilder[r] }
                    )
                    .ToArray();

                rulesByProducedBuilder.Remove(kvp.Key);

                ruleMappingBuilder.RemoveRange(ruleMapping.Select(m => m.origRule));
                ruleMappingBuilder.AddRange(ruleMapping.Select(t => new KeyValuePair<Rule, Rule>(t.newRule, t.mappedRule)));

                var aliasingRuleIndex = aliasOfRulesBuilder.FindIndex(r => r.Symbols.Count == 1 && r.Symbols.Single() == kvp.Key);                
                aliasOfRulesBuilder.RemoveAt(aliasingRuleIndex);
                aliasOfRulesBuilder.InsertRange(aliasingRuleIndex, ruleMapping.Select(t => t.newRule));

                var rightAssociativeRuleMapping = ruleMapping.Where(t => rightAssociativeRulesBuilder.Contains(t.origRule))
                    .ToArray();
                rightAssociativeRulesBuilder.ExceptWith(rightAssociativeRuleMapping.Select(t => t.origRule));
                rightAssociativeRulesBuilder.UnionWith(rightAssociativeRuleMapping.Select(t => t.newRule));
            }

            return new LeftRecursionRewriter(
                rulesByProducedBuilder.ToImmutable(),
                ruleMappingBuilder.ToImmutable(),
                ImmutableDictionary<NonTerminal, NonTerminal>.Empty, // all aliases removed
                rightAssociativeRulesBuilder.ToImmutable()
            );
        }
        #endregion
    }
}
