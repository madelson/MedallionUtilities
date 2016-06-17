using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class CommandLineSyntax : CommandSyntax
    {
        internal CommandLineSyntax(
            string name, 
            string description, 
            IEnumerable<ArgumentSyntax> arguments,
            IEnumerable<SubCommandSyntax> subCommands)
            : base(name, description, arguments, subCommands)
        {
        }

        public static ArgumentSyntax<bool> FlagArgument(string name, string shortName = null, string description = null) 
           => new ArgumentSyntax<bool>(name: name, shortName: shortName, description: description, kind: ArgumentSyntaxKind.Flag, required: false, parser: null, validator: null);

        public static ArgumentSyntax<T> PositionalArgument<T>(
            string name,
            string description = null,
            Func<string, T> parser = null,
            Action<T> validator = null,
            bool required = true)
            => new ArgumentSyntax<T>(name: name, shortName: null, description: description, kind: ArgumentSyntaxKind.Positional, required: required, parser: parser, validator: validator);

        public static ArgumentSyntax<string> PositionalArgument(string name, string description = null, Action<string> validator = null, bool required = true)
            => PositionalArgument<string>(name: name, description: description, validator: validator, required: required);

        public static ArgumentSyntax<T> NamedArgument<T>(
            string name,
            string shortName = null,
            string description = null,
            Func<string, T> parser = null,
            Action<T> validator = null,
            bool required = false)
            => new ArgumentSyntax<T>(name: name, shortName: shortName, description: description, kind: ArgumentSyntaxKind.Named, required: required, parser: parser, validator: validator);

        public static ArgumentSyntax<string> NamedArgument(
            string name,
            string shortName = null,
            string description = null,
            Action<string> validator = null,
            bool required = false)
            => NamedArgument<string>(name: name, shortName: shortName, description: description, validator: validator, required: required);

        public static CommandLineSyntax Command(
            string name,
            string description = null,
            IEnumerable<ArgumentSyntax> arguments = null,
            IEnumerable<SubCommandSyntax> subCommands = null)
            => new CommandLineSyntax(name, description, arguments ?? Enumerable.Empty<ArgumentSyntax>(), subCommands ?? Enumerable.Empty<SubCommandSyntax>());

        public static SubCommandSyntax SubCommand(
            string name,
            string description = null,
            IEnumerable<ArgumentSyntax> arguments = null,
            IEnumerable<SubCommandSyntax> subCommands = null)
            => new SubCommandSyntax(name, description, arguments, subCommands);
    }
}
