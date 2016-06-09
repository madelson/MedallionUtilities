using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ParsedSubCommandSyntax : ParsedCommandLineSyntax
    {
        internal ParsedSubCommandSyntax(ArraySegment<string> tokens, SubCommandSyntax subCommand, ParsedCommandSyntax command)
            : base(tokens)
        {
            if (subCommand == null) { throw new ArgumentNullException(nameof(subCommand)); }
            if (command == null) { throw new ArgumentNullException(nameof(command)); }
            if (subCommand.Command != command.Command) { throw new ArgumentException($"the {nameof(subCommand)} and {nameof(command)} must be associated", nameof(command)); }

            this.SubCommand = subCommand;
            this.Command = command;
        }

        public SubCommandSyntax SubCommand { get; }
        public ParsedCommandSyntax Command { get; }
    }
}
