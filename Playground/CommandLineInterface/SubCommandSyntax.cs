using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class SubCommandSyntax : CommandLineSyntax
    {
        public SubCommandSyntax(string name, CommandSyntax command)
        {
            if (string.IsNullOrEmpty(this.Name)) { throw new ArgumentException("must not be null or empty", nameof(name)); }
            if (command == null) { throw new ArgumentNullException(nameof(command)); }

            this.Name = name;
            this.Command = command;
        }

        public string Name { get; }
        public new CommandSyntax Command { get; }
    }
}
