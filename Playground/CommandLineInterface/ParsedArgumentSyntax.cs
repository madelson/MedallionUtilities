using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ParsedArgumentSyntax : ParsedCommandLineSyntax
    {
        internal ParsedArgumentSyntax(ArraySegment<string> tokens, ArgumentSyntax argument, object value)
            : base(tokens)
        {
            if (argument == null) { throw new ArgumentNullException(nameof(argument)); }
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            this.Argument = argument;
            this.Value = value;
        }

        public ArgumentSyntax Argument { get; }
        public object Value { get; }
    }
}
