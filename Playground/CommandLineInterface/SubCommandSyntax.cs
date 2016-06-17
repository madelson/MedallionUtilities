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
            string description,
            IEnumerable<ArgumentSyntax> arguments,
            IEnumerable<SubCommandSyntax> subCommands)
            : base(name, description, arguments, subCommands)
        {
        }
    }
}
