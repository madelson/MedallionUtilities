using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public abstract class CommandLineSyntax
    {
        internal CommandLineSyntax() { }

        public static ArgumentSyntax<bool> FlagArgument(string name) => new FlagArgumentSyntax(name);

        public static ArgumentSyntax<T> PositionalArgument<T>(
            Func<string, IFormatProvider, T> parser = null,
            Action<T> validator = null,
            bool required = true)
            => new ParsedArgumentSyntax<T>(required, name: null, parser: parser, validator: validator);

        public static ArgumentSyntax<string> PositionalArgument(Action<string> validator = null, bool required = true)
            => PositionalArgument<string>(validator: validator, required: required); 

        public static ArgumentSyntax<T> NamedArgument<T>(
            string name,
            Func<string, IFormatProvider, T> parser = null,
            Action<T> validator = null,
            bool required = false)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }

            return new ParsedArgumentSyntax<T>(required, name, parser, validator);
        }

        public static ArgumentSyntax<string> NamedArgument(
            string name,
            Action<string> validator = null,
            bool required = false)
            => NamedArgument<string>(name, validator: validator, required: required);

        private sealed class FlagArgumentSyntax : ArgumentSyntax<bool>
        {
            public FlagArgumentSyntax(string name)
                : base(required: false)
            {
                if (string.IsNullOrEmpty(name)) { throw new ArgumentException("must not be null or empty", nameof(name)); }

                this.Name = name;
            }

            public override string Name { get; }
            public override bool IsFlag => true;
            internal override object Parse(string text, IFormatProvider provider) { throw new InvalidOperationException("Flags do not require parsing"); }
            internal override void Validate(object value) { }
        }

        private sealed class ParsedArgumentSyntax<T> : ArgumentSyntax<T>
        {
            private readonly Func<string, IFormatProvider, T> parser;
            private readonly Action<T> validator;

            public ParsedArgumentSyntax(
                bool required,
                string name,
                Func<string, IFormatProvider, T> parser,
                Action<T> validator)
                : base(required)
            {
                if (name == string.Empty) { throw new ArgumentException("may not be empty", nameof(name)); }

                this.Name = name;
                this.parser = parser ?? GetDefaultParser();
                this.validator = validator;
            }

            public override bool IsFlag => false;
            public override string Name { get; }

            internal override object Parse(string text, IFormatProvider provider) => this.parser(text, provider);

            internal override void Validate(object value) => this.validator?.Invoke((T)value);

            internal static Func<string, IFormatProvider, T> GetDefaultParser()
            {
                var parser = GetDefaultParser(typeof(T));
                return (s, f) => (T)parser(s, f);
            }

            private static Func<string, IFormatProvider, object> GetDefaultParser(Type type)
            {
                var underlyingType = Nullable.GetUnderlyingType(type);
                if (underlyingType != null) { return GetDefaultParser(underlyingType); }

                if (type.IsEnum)
                {
                    var isFlags = type.IsDefined(typeof(FlagsAttribute), inherit: false);
                    return (s, f) =>
                    {
                        var parsed = Enum.Parse(type, s, ignoreCase: true);
                        // based on http://stackoverflow.com/questions/2674730/is-there-a-way-to-check-if-int-is-legal-enum-in-c
                        char firstChar;
                        if ((isFlags && (char.IsDigit(firstChar = parsed.ToString()[0]) || firstChar == '-'))
                            || !Enum.IsDefined(type, parsed))
                        {
                            throw new InvalidCastException($"'{s}' is not a valid value of {type}");
                        }

                        return parsed;
                    };
                }

                if (typeof(IConvertible).IsAssignableFrom(type))
                {
                    return (s, f) => Convert.ChangeType(s, type, f);
                }

                var parseMethodWithFormatProvider = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, Type.DefaultBinder, new[] { typeof(string), typeof(IFormatProvider) }, new ParameterModifier[0]);
                if (parseMethodWithFormatProvider != null)
                {
                    return (s, f) => parseMethodWithFormatProvider.Invoke(null, new object[] { s, f });
                }

                var parseMethodWithoutFormatProvider = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, Type.DefaultBinder, new[] { typeof(string) }, new ParameterModifier[0]);
                if (parseMethodWithoutFormatProvider != null)
                {
                    return (s, _) => parseMethodWithoutFormatProvider.Invoke(null, new object[] { s });
                }

                throw new ArgumentException($"Could not construct a default parser for {type}: must have a static Parse method or be a convertible or enum type");
            }
        }
    }
}
