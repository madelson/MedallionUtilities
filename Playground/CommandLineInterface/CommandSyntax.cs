using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public class CommandSyntax
    {
        internal CommandSyntax(IEnumerable<SubCommandSyntax> subCommands, IEnumerable<ArgumentSyntax> arguments)
        {
            if (subCommands == null) { throw new ArgumentNullException(nameof(subCommands)); }
            if (arguments == null) { throw new ArgumentNullException(nameof(arguments)); }

            this.SubCommands = subCommands.ToArray();
            if (this.SubCommands.Contains(null)) { throw new ArgumentNullException("must all be non-null", nameof(subCommands)); }

            this.Arguments = arguments.ToArray();
            if (this.Arguments.Contains(null)) { throw new ArgumentNullException("must all be non-null", nameof(arguments)); }
            // todo validate optional positioning 
        }

        public IReadOnlyList<SubCommandSyntax> SubCommands { get; }
        public IReadOnlyList<ArgumentSyntax> Arguments { get; }
    }
}
