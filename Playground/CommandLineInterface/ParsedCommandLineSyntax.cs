using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public abstract class ParsedCommandLineSyntax
    {
        internal ParsedCommandLineSyntax(ArraySegment<string> tokens)       
        {
            if (tokens.Array == null) { throw new ArgumentNullException("array must be non-null", nameof(tokens)); }

            this.Tokens = tokens;
        }

        public ArraySegment<string> Tokens { get; }
    }
}
