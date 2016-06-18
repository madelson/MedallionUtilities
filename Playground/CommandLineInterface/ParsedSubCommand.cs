using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ParsedSubCommand : ParsedCommandLineElement
    {
        internal ParsedSubCommand(ArraySegment<string> tokens, SubCommandSyntax syntax, ParsedSubCommand subCommand, IEnumerable<ParsedArgument> arguments)
            : base(tokens, syntax)
        {
            if (arguments == null) { throw new ArgumentNullException(nameof(arguments)); }

            this.SubCommand = subCommand;
            this.Arguments = new ParsedArgumentCollection(syntax, arguments);
        }

        public new SubCommandSyntax Syntax => (SubCommandSyntax)base.Syntax;
        public ParsedSubCommand SubCommand { get; }
        public ParsedArgumentCollection Arguments { get; }
    }
}
