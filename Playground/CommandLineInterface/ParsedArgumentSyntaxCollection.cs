using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class ParsedArgumentSyntaxCollection : IReadOnlyCollection<ParsedArgumentSyntax>
    {
        private readonly CommandSyntax command;
        private readonly IReadOnlyList<ParsedArgumentSyntax> arguments;

        internal ParsedArgumentSyntaxCollection(CommandSyntax command, IEnumerable<ParsedArgumentSyntax> arguments)
        {
            if (command == null) { throw new ArgumentNullException(nameof(command)); }
            if (arguments == null) { throw new ArgumentNullException(nameof(arguments)); }

            this.command = command;

            this.arguments = arguments.ToArray();
            if (arguments.Contains(null)) { throw new ArgumentNullException("must all be non-null", nameof(arguments)); }
            if (arguments.Select(a => a.Argument).Except(command.Arguments).Any())
            {
                throw new ArgumentException($"must all belong to the current command", nameof(arguments));
            }
        }

        public int Count => this.arguments.Count;
        public IEnumerator<ParsedArgumentSyntax> GetEnumerator() => this.arguments.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool TryGetValue(ArgumentSyntax argument, out object value) => TryGetValue(Find(argument), out value);
        public bool TryGetValue<T>(ArgumentSyntax<T> argument, out T value) => TryGetValue(Find(argument), out value);
        public bool TryGetValue(string name, out object value) => TryGetValue(Find(name), out value);
        public bool TryGetValue(int positionalIndex, out object value) => TryGetValue(Find(positionalIndex), out value);

        private bool TryGetValue<T>(ParsedArgumentSyntax parsedArgument, out T value)
        {
            if (parsedArgument != null)
            {
                value = (T)parsedArgument.Value;
                return true;
            }

            value = default(T);
            return false;
        }

        public object this[ArgumentSyntax argument] => GetValue<object>(Find(argument));
        public T GetValue<T>(ArgumentSyntax<T> argument) => GetValue<T>(Find(argument));
        public object this[string name] => GetValue<object>(Find(name));
        public object GetValue(int positionalIndex) => GetValue<object>(Find(positionalIndex));

        private T GetValue<T>(ParsedArgumentSyntax parsedArgument)
        {
            if (parsedArgument == null) { throw new KeyNotFoundException("no value was provided for the argument"); }

            return (T)parsedArgument.Value;
        }

        public object GetValueOrDefault(ArgumentSyntax argument) => GetValueOrDefault<object>(Find(argument));
        public T GetValueOrDefault<T>(ArgumentSyntax<T> argument) => GetValueOrDefault<T>(Find(argument));
        public object GetValueOrDefault(string name) => GetValueOrDefault<object>(Find(name));
        public object GetValueOrDefault(int positionalIndex) => GetValueOrDefault<object>(Find(positionalIndex));

        private T GetValueOrDefault<T>(ParsedArgumentSyntax parsedArgument) => parsedArgument != null ? (T)parsedArgument.Value : default(T);

        private ParsedArgumentSyntax Find(ArgumentSyntax argument)
        {
            if (argument == null) { throw new ArgumentNullException(nameof(argument)); }

            var found = this.arguments.FirstOrDefault(a => a.Argument == argument);
            if (found != null) { return found; }

            if (!this.command.Arguments.Contains(argument))
            {
                throw new ArgumentException("the provided argument must be associated with the parsed command", nameof(argument));
            }

            return null;
        }

        private ParsedArgumentSyntax Find(string name)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }

            // todo case
            var found = this.arguments.FirstOrDefault(a => a.Argument.Name == name);
            if (found != null) { return found; }

            // todo case
            if (!this.command.Arguments.Any(a => a.Name == name))
            {
                throw new ArgumentException($"'{name}' does not belong to any argument in the parsed command", nameof(name));
            }

            return null;
        }

        private ParsedArgumentSyntax Find(int positionalIndex)
        {
            if (positionalIndex < 0) { throw new ArgumentOutOfRangeException(nameof(positionalIndex), positionalIndex, "must be non-negative"); }

            var found = this.arguments.Where(a => a.Argument.Name == null).ElementAtOrDefault(positionalIndex);
            if (found != null) { return found; }

            var positionalArgumentCount = this.arguments.Count(a => a.Argument.Name == null);
            if (positionalIndex >= positionalArgumentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(positionalIndex), positionalIndex, $"the associated command has only {positionalArgumentCount} positional arguments");
            }

            return null;
        }
    }
}
