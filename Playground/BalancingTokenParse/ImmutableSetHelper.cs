using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    internal static class ImmutableSetHelper
    {
        // workarounds for https://github.com/dotnet/corefx/issues/16948

        private static class DefaultSet<T>
        {
            public static readonly ImmutableHashSet<T> Instance = ImmutableHashSet.CreateRange(new[] { default(T) });
        }

        public static ImmutableHashSet<T> GetDefaultSet<T>() => DefaultSet<T>.Instance;

        public static IImmutableSet<T> AddDefault<T>(this IImmutableSet<T> set)
        {
            return set.Union(DefaultSet<T>.Instance);
        }

        public static IImmutableSet<T> RemoveDefault<T>(this IImmutableSet<T> set)
        {
            return set.Except(DefaultSet<T>.Instance);
        }

        public static bool ContainsDefault<T>(this IImmutableSet<T> set)
        {
            return set.Except(DefaultSet<T>.Instance).Count < set.Count;
        }
    }
}
