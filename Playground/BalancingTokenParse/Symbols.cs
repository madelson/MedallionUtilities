using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.BalancingTokenParse
{
    abstract class Symbol
    {
        protected Symbol(string name)
        {
            this.Name = name;
        }

        public string Name { get; }

        public override string ToString() => this.Name;
    }

    class Token : Symbol
    {
        public static readonly Token Eof = new Token("EOF");

        public Token(string name) : base(name) { }
    }

    class NonTerminal : Symbol
    {
        public NonTerminal(string name) : base(name) { }
    }
}
