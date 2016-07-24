using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    internal sealed class CommandLineParser
    {
        private NamedArgumentStyles namedArgumentStyles;

        public CommandLineParser(NamedArgumentStyles namedArgumentStyles)
        {
            this.namedArgumentStyles = namedArgumentStyles;
        }

        public ParsedCommandLine Parse(string[] commandLine, bool includesExecutableName, CommandLineSyntax syntax)
        {
            if (commandLine == null) { throw new ArgumentNullException(nameof(commandLine)); }
            if (commandLine.Contains(null)) { throw new ArgumentNullException("must all be non-null", nameof(commandLine)); }
            if (syntax == null) { throw new ArgumentNullException(nameof(syntax)); }

            return this.ParseCommand(new ArraySegment<string>(commandLine), syntax, (t, s, sc, args) => new ParsedCommandLine(t, s, sc, args));
        }

        private TParsed ParseCommand<TParsed, TSyntax>(
            ArraySegment<string> tokens,
            TSyntax syntax,
            Func<ArraySegment<string>, TSyntax, ParsedSubCommand, IEnumerable<ParsedArgument>, TParsed> factory)
            where TParsed : ParsedCommandLineElement
            where TSyntax : CommandSyntax
        {
            var namedArguments = syntax.Arguments.Where(a => a.Kind != ArgumentSyntaxKind.Positional).ToArray();
            var positionalArguments = syntax.Arguments.Where(a => a.Kind == ArgumentSyntaxKind.Positional).ToArray();

            ParsedSubCommand parsedSubCommand = null;
            var parsedNamedArgments = new List<ParsedArgument>();
            var positionalArgumentTokens = new List<ArraySegment<string>>();

            var isLookingForSubCommand = syntax.SubCommands.Any();
            var i = 0;
            while (i < tokens.Count)
            {
                // first, check if we found a sub command
                if (isLookingForSubCommand && TryParseAny(SubSegment(tokens, i), this.TryParseSubCommand, syntax.SubCommands, out parsedSubCommand))
                {
                    isLookingForSubCommand = false; // can only have one sub command
                    i += parsedSubCommand.Tokens.Count;
                    continue;
                }

                // next, see if we found a named argument
                ParsedArgument parsedArgument;
                if (TryParseAny(SubSegment(tokens, i), this.TryParseNamedArgument, namedArguments, out parsedArgument))
                {
                    parsedNamedArgments.Add(parsedArgument);
                    i += parsedArgument.Tokens.Count;
                    continue;
                }

                // TODO we should probably attempt to parse additional unknown named arguments here based on
                // the current style

                // we must have found a positional argument. We can't parse yet because
                // we have to look at which end of the positional argument list is optional
                positionalArgumentTokens.Add(SubSegment(tokens, i, 1));
                isLookingForSubCommand = false; // sub commands can't appear after positional arguments
                ++i;
            }

            // validate whether any named arguments were required but unspecified
            var missingRequiredNamedArgument = namedArguments.Except(parsedNamedArgments.Select(a => a.Syntax))
                .FirstOrDefault(a => a.Required);
            if (missingRequiredNamedArgument != null)
            {
                throw new CommandLineParseException(
                    $"Argument '{missingRequiredNamedArgument.Name}' is required but was not specified",
                    SubSegment(tokens, t)
                );
            }

            // now we can parse the positional arguments. If we have multiple positional arguments and the
            // first argument is optional, then we must parse backwards
            var scanBackwards = positionalArguments.Length > 1 && !positionalArguments[0].Required;
            var parsedPositionalArguments = (scanBackwards ? positionalArguments.Reverse() : positionalArguments)
                .Zip(
                    scanBackwards ? positionalArgumentTokens.AsEnumerable().Reverse() : positionalArgumentTokens,
                    (a, t) => ParsePositionalArgument(t, a)
                )
                .ToArray();
            if (parsedPositionalArguments.Length < positionalArgumentTokens.Count)
            {
                var extraPositionalArguments = scanBackwards
                    ? positionalArgumentTokens.Take(positionalArgumentTokens.Count - parsedPositionalArguments.Length).ToArray()
                    : positionalArgumentTokens.Skip(parsedPositionalArguments.Length).ToArray();

                throw new CommandLineParseException(
                    $"Unrecognized extra positional arguments {string.Join(", ", extraPositionalArguments.SelectMany(t => $"'{t}'"))}",
                    new ArraySegment<string>(tokens.Array, extraPositionalArguments[0].Offset, extraPositionalArguments.Last().Offset + extraPositionalArguments.Last().Count - extraPositionalArguments[0].Offset)
                );
            }

            var parsedArguments = parsedNamedArgments.Concat(parsedPositionalArguments)
                .OrderBy(a => a.Tokens.Offset);

            return factory(tokens, syntax, parsedSubCommand, parsedArguments);
        }

        private delegate bool SyntaxParser<TSyntax, TParsed>(ArraySegment<string> tokens, TSyntax syntax, out TParsed parsed);

        private static bool TryParseAny<TSyntax, TParsed>(ArraySegment<string> tokens, SyntaxParser<TSyntax, TParsed> parser, IReadOnlyCollection<TSyntax> syntaxes, out TParsed parsed)
        {
            foreach (var syntax in syntaxes)
            {
                if (parser(tokens, syntax, out parsed)) { return true; }
            }

            parsed = default(TParsed);
            return false;
        }

        private bool TryParseSubCommand(ArraySegment<string> tokens, SubCommandSyntax syntax, out ParsedSubCommand parsed)
        {
            // todo case
            if (tokens.Count == 0 || tokens.First() != syntax.Name)
            {
                parsed = null;
                return false;
            }

            var parsedCommand = this.ParseCommand(SubSegment(tokens, 1), syntax.Command);
            parsed = new ParsedSubCommand(tokens, syntax, parsedCommand);
            return true;
        }

        private bool TryParseNamedArgument(ArraySegment<string> tokens, ArgumentSyntax syntax, out ParsedArgument parsed)
        {
            throw new NotImplementedException();
        }

        private ParsedArgument ParsePositionalArgument(ArraySegment<string> tokens, ArgumentSyntax syntax)
        {
            if (tokens.Count != 1) { throw new InvalidOperationException("should never get here"); }

            var value = ParseAndValidateValue(tokens, syntax);
            return new ParsedArgument(tokens, syntax, value);
        }

        private object ParseAndValidateValue(ArraySegment<string> token, ArgumentSyntax syntax)
        {
            object parsed;
            try { parsed = syntax.Parse(token.Single()); }
            catch (Exception ex)
            {
                throw new CommandLineParseException(
                    $"Failed to parse argument '{token.Single()}' as type {syntax.Type}{(syntax.Name != null ? $" for '{syntax.Name}'" : string.Empty)}",
                    token,
                    ex
                );
            }

            try { syntax.Validate(parsed); }
            catch (Exception ex)
            {
                throw new CommandLineParseException(
                    $"Invalid argument value '{parsed}'{(syntax.Name != null ? $" for '{syntax.Name}'" : string.Empty)}",
                    token,
                    ex
                );
            }

            return parsed;
        }

        private static ArraySegment<T> SubSegment<T>(ArraySegment<T> segment, int start, int? count = null)
        {
            if (segment.Array == null) { throw new ArgumentNullException("array must be non-null", nameof(segment)); }
            if (start < 0 || start > segment.Count) { throw new ArgumentOutOfRangeException(nameof(start), start, $"must be in [0, {segment.Count}]"); }
            if (count.HasValue)
            {
                if (count.Value < 0) { throw new ArgumentOutOfRangeException(nameof(count), count, "must be non-negative"); }
                if (start + count.Value > segment.Count) { throw new ArgumentOutOfRangeException(nameof(count), count, $"{nameof(start)} ({start}) + {nameof(count)} must not go past the end of {nameof(segment)}"); }
            }

            return new ArraySegment<T>(segment.Array, offset: segment.Offset + start, count: count ?? (segment.Count - start));
        }
    }
}
