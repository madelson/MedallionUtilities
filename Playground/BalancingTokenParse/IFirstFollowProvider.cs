using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    interface IFirstFollowProvider
    {
        IImmutableSet<Token> FirstOf(Symbol symbol);
        IImmutableSet<Token> FollowOf(Symbol symbol);
        IImmutableSet<Token> FollowOf(Rule rule);
    }

    static class FirstFollowProviderExtensions
    {
        public static bool IsNullable(this IFirstFollowProvider provider, Symbol symbol)
        {
            return symbol is NonTerminal && provider.FirstOf(symbol).ContainsDefault();
        }

        public static IImmutableSet<Token> FirstOf(this IFirstFollowProvider provider, IEnumerable<Symbol> symbols)
        {
            var builder = ImmutableHashSet.CreateBuilder<Token>();
            foreach (var symbol in symbols)
            {
                var firsts = provider.FirstOf(symbol);
                builder.UnionWith(firsts.Where(s => s != null));
                if (!firsts.ContainsDefault())
                {
                    // not nullable
                    return builder.ToImmutable();
                }
            }

            // if we reach here, we're nullable
            return builder.ToImmutable().AddDefault();
        }

        public static IImmutableSet<Token> NextOf(this IFirstFollowProvider provider, Rule rule)
        {
            return provider.NextOf(new PartialRule(rule));
        }

        public static IImmutableSet<Token> NextOf(this IFirstFollowProvider provider, PartialRule partialRule)
        {
            var firsts = provider.FirstOf(partialRule.Symbols);
            return firsts.ContainsDefault()
                ? firsts.RemoveDefault().Union(provider.FollowOf(partialRule.Rule))
                : firsts;
        }
    }
}
