using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ParsedCommand : ParsedCommandLineElement
    {
        internal ParsedCommand(ArraySegment<string> tokens, CommandLineSyntax syntax, ParsedSubCommand subCommand, IEnumerable<ParsedArgument> arguments)
            : base(tokens, syntax)
        {
            if (arguments == null) { throw new ArgumentNullException(nameof(arguments)); }
            
            this.SubCommand = subCommand;
            this.Arguments = new ParsedArgumentCollection(syntax, arguments);
        }

        public new CommandLineSyntax Syntax => (CommandLineSyntax)base.Syntax;
        public ParsedSubCommand SubCommand { get; }
        public ParsedArgumentCollection Arguments { get; }
    }
}
