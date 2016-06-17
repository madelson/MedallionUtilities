using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public abstract class ArgumentSyntax : CommandLineElementSyntax
    {
        internal ArgumentSyntax(string name, string shortName, string description, ArgumentSyntaxKind kind, bool required)
            : base(name, description)
        {
            if (shortName != null && shortName.Length == 0) { throw new ArgumentException("must not be empty", nameof(shortName)); }
            if (kind < ArgumentSyntaxKind.Positional || kind > ArgumentSyntaxKind.Flag)
            {
                throw new ArgumentException($"Unknown {nameof(ArgumentSyntaxKind)} '{kind}'", nameof(kind));
            }
            if (required && kind == ArgumentSyntaxKind.Flag) { throw new ArgumentException("Flag arguments cannot be required", nameof(required)); }
            if (kind == ArgumentSyntaxKind.Flag && this.Type == typeof(bool)) { throw new ArgumentException("Flag arguments must be boolean type", nameof(kind)); }
            if (kind == ArgumentSyntaxKind.Positional && shortName != null) { throw new ArgumentException("Positional arguments may not have a short name", nameof(shortName)); }

            this.ShortName = shortName;
            this.Kind = kind;
            this.Required = required;
        }

        public string ShortName { get; }
        public ArgumentSyntaxKind Kind { get; }
        public bool Required { get; }

        public abstract Type Type { get; }
        internal abstract object Parse(string text);
        internal abstract void Validate(object value);
    }

    public enum ArgumentSyntaxKind
    {
        Positional = 0,
        Named = 1,
        Flag = 2,
    }

    public sealed class ArgumentSyntax<T> : ArgumentSyntax
    {
        private Func<string, T> parser;
        private Action<T> validator;

        internal ArgumentSyntax(
            string name,
            string shortName,
            string description,
            ArgumentSyntaxKind kind,
            bool required,
            Func<string, T> parser,
            Action<T> validator)
            : base(name, shortName, description, kind, required)
        {
            this.parser = parser;
            this.validator = validator;
        }
        
        public override Type Type => typeof(T);

        internal override object Parse(string text)
        {
            return this.parser != null
                ? this.parser(text)
                : DefaultParse(text);
        }

        internal override void Validate(object value)
        {
            this.validator?.Invoke((T)value);
        }

        private object DefaultParse(string text)
        {
            var underlyingType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (underlyingType.IsEnum)
            {
                var helper = EnumDescriptor.ForType(underlyingType);
                return helper.Parse(text);
            }

            return TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(text);
        }
    }
}
