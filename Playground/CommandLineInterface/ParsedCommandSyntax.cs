using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ParsedCommandSyntax : ParsedCommandLineSyntax
    {
        internal ParsedCommandSyntax(ArraySegment<string> tokens, CommandSyntax command, ParsedSubCommandSyntax subCommand, IEnumerable<ParsedArgumentSyntax> arguments)
            : base(tokens)
        {
            if (command == null) { throw new ArgumentNullException(nameof(command)); }
            if (arguments == null) { throw new ArgumentNullException(nameof(arguments)); }

            this.Command = command;
            this.SubCommand = subCommand;
            this.Arguments = new ParsedArgumentSyntaxCollection(command, arguments);
        }

        public CommandSyntax Command { get; }
        public ParsedSubCommandSyntax SubCommand { get; }
        public ParsedArgumentSyntaxCollection Arguments { get; }
    }
}
