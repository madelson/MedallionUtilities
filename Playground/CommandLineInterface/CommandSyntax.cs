using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public abstract class CommandSyntax : CommandLineElementSyntax
    {
        internal CommandSyntax(
            string name,
            string description,
            IEnumerable<ArgumentSyntax> arguments, 
            IEnumerable<SubCommandSyntax> subCommands)
            : base(name, description)
        {
            if (arguments == null) { throw new ArgumentNullException(nameof(arguments)); }
            if (subCommands == null) { throw new ArgumentNullException(nameof(subCommands)); }

            this.Arguments = arguments.ToArray();
            if (this.Arguments.Contains(null)) { throw new ArgumentNullException("must all be non-null", nameof(arguments)); }
            var positionalArguments = this.Arguments.Where(a => a.Name == null).ToArray();
            if (positionalArguments.Length > 1)
            {
                if ((
                        !positionalArguments[0].Required
                        && positionalArguments.SkipWhile(p => !p.Required).Any(p => !p.Required)
                    )
                    ||
                    (
                        !positionalArguments[positionalArguments.Length - 1].Required
                        && positionalArguments.Reverse().SkipWhile(p => !p.Required).Any(p => !p.Required)
                    ))
                {
                    throw new ArgumentException(
                        "optional positional arguments must either all appear at the end or all appear at the beginning of the positional argument list",
                        nameof(arguments)
                    );
                }
            }

            this.SubCommands = subCommands.ToArray();
            if (this.SubCommands.Contains(null)) { throw new ArgumentNullException("must all be non-null", nameof(subCommands)); }
        }

        public IReadOnlyList<ArgumentSyntax> Arguments { get; }
        public IReadOnlyList<SubCommandSyntax> SubCommands { get; }
    }
}
