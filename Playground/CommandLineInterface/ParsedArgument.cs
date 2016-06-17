using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ParsedArgument : ParsedCommandLineElement
    {
        internal ParsedArgument(ArraySegment<string> tokens, ArgumentSyntax syntax, object value)
            : base(tokens, syntax)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }

            this.Value = value;
        }

        public new ArgumentSyntax Syntax => (ArgumentSyntax)base.Syntax;
        public object Value { get; }
    }
}
