using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    class Rule
    {
        public Rule(NonTerminal produced, params Symbol[] symbols) 
            : this(produced, (IEnumerable<Symbol>)symbols)
        {
        }

        public Rule(NonTerminal produced, IEnumerable<Symbol> symbols)
        {
            this.Produced = produced;
            this.Symbols = symbols.ToArray();
        }

        public NonTerminal Produced { get; }
        public IReadOnlyList<Symbol> Symbols { get; }

        public override string ToString() => $"{this.Produced} -> {string.Join(" ", this.Symbols)}";
    }
}
