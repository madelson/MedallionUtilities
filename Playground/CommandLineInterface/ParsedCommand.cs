using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ParsedCommand : ParsedCommandLineElement
    {
        internal ParsedCommand(ArraySegment<string> tokens, CommandSyntax command, ParsedSubCommand subCommand, IEnumerable<ParsedArgument> arguments)
            : base(tokens)
        {
            if (command == null) { throw new ArgumentNullException(nameof(command)); }
            if (arguments == null) { throw new ArgumentNullException(nameof(arguments)); }

            this.Command = command;
            this.SubCommand = subCommand;
            this.Arguments = new ParsedArgumentCollection(command, arguments);
        }

        public CommandSyntax Command { get; }
        public ParsedSubCommand SubCommand { get; }
        public ParsedArgumentCollection Arguments { get; }
    }
}
