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

        public Rule(NonTerminal produced, IEnumerable<Symbol> symbols, RuleAction action = null, string requiredParserVariable = null)
        {
            this.Produced = produced;
            this.Symbols = symbols.ToArray();
            this.Action = action;
            this.RequiredParserVariable = requiredParserVariable;

            if (this.Symbols.Count == 1 && this.Symbols[0] == this.Produced)
            {
                throw new ArgumentException("cannot be of the form S -> S", nameof(symbols));
            }
        }

        public NonTerminal Produced { get; }
        public IReadOnlyList<Symbol> Symbols { get; }

        public string RequiredParserVariable { get; }
        public RuleAction Action { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(this.Produced).Append(" -> ").Append(string.Join(" ", this.Symbols));

            if (this.RequiredParserVariable != null || this.Action != null)
            {
                sb.Append(" {");

                if (this.RequiredParserVariable != null)
                {
                    sb.Append(" REQUIRE ").Append(this.RequiredParserVariable);
                }
                if (this.Action != null)
                {
                    sb.Append(' ').Append(this.Action);
                }

                sb.Append(" }");
            }

            return sb.ToString();
        }
    }    

    // TODO I don't think this has to support prefix rules at all, just suffix rules. We might want to support
    // common suffix as a parse node in the future; unclear how valuable this is, though (other than reducing tree size)
    class PartialRule
    {
        private IReadOnlyList<Symbol> cachedSymbols;

        public PartialRule(Rule rule, int start = 0, int? length = null)
        {
            if (start < 0 || start > rule.Symbols.Count) { throw new ArgumentOutOfRangeException(nameof(start), start, "must be in range of " + nameof(rule)); }
            if (length.HasValue && (length.Value < 0 || length.Value > rule.Symbols.Count - start)) { throw new ArgumentOutOfRangeException(nameof(length), length, "must be in range of " + nameof(rule)); }
            
            this.Rule = rule;
            this.Start = start;
            this.Length = length ?? (rule.Symbols.Count - start);
        }

        public PartialRule(PartialRule rule, int start = 0, int? length = null)
            : this(rule.Rule, start + rule.Start, length ?? (rule.Length - start))
        {
            if (length.HasValue && length.Value > rule.Length) { throw new ArgumentOutOfRangeException(nameof(length), length, "must be in range of " + nameof(rule)); } 
        }

        public Rule Rule { get; }
        public int Start { get; }
        public int Length { get; }

        public NonTerminal Produced => this.Rule.Produced;
        public IReadOnlyList<Symbol> Symbols
        {
            get
            {
                if (this.cachedSymbols == null)
                {
                    if (this.Start == 0 && this.Length == this.Rule.Symbols.Count)
                    {
                        this.cachedSymbols = this.Rule.Symbols;
                    }
                    else
                    {
                        this.cachedSymbols = this.Rule.Symbols.Skip(this.Start).Take(this.Length).ToArray();
                    }
                }

                return this.cachedSymbols;
            }
        }

        public override bool Equals(object obj)
        {
            var that = obj as PartialRule;
            return that != null && that.Rule == this.Rule && that.Start == this.Start && that.Length == this.Length;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = this.Rule.GetHashCode();
                hash = (3 * hash) + this.Start.GetHashCode();
                hash = (3 * hash) + this.Length.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{this.Produced} -> {(this.Start > 0 ? "... " : string.Empty)}{string.Join(" ", this.Symbols)}{(this.Start + this.Length < this.Rule.Symbols.Count ? " ... " : string.Empty)}";
        }
    }

    class RuleAction
    {
        public RuleAction(string variable, RuleActionKind kind)
        {
            this.Variable = variable;
            this.Kind = kind;
        }

        public string Variable { get; }
        public RuleActionKind Kind { get; }

        public override string ToString() => $"{this.Kind} {this.Variable}";
    }

    enum RuleActionKind
    {
        Push,
        Set,
        Pop,
    } 
}
