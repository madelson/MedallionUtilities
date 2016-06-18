using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public abstract class ParsedCommandLineElement
    {
        internal ParsedCommandLineElement(ArraySegment<string> tokens, CommandLineElementSyntax syntax)
        {
            if (tokens.Array == null) { throw new ArgumentNullException("array must be non-null", nameof(tokens)); }
            if (syntax == null) { throw new ArgumentNullException(nameof(syntax)); }

            this.Tokens = tokens;
            this.Syntax = syntax;
        }

        public ArraySegment<string> Tokens { get; }
        public CommandLineElementSyntax Syntax { get; }
    }
}
