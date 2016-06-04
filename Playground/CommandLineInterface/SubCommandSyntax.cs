using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class SubCommandSyntax : CommandSyntax
    {
        public SubCommandSyntax(
            string name,
            IEnumerable<SubCommandSyntax> subCommands,
            IEnumerable<ArgumentSyntax> arguments)
            : base(subCommands, arguments)
        {
            if (string.IsNullOrEmpty(this.Name)) { throw new ArgumentException("must not be null or empty", nameof(name)); }

            this.Name = name;
        }

        public string Name { get; }
    }
}
