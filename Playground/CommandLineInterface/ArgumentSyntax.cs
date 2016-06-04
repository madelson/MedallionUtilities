using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public abstract class ArgumentSyntax : CommandLineSyntax
    {
        internal ArgumentSyntax() { }

        public abstract string Name { get; }
        public abstract Type Type { get; }
        internal abstract object Parse(string text, IFormatProvider provider);
        internal abstract void Validate(object value);
        public abstract bool Required { get; }
        public abstract bool IsFlag { get; }
    }

    public abstract class ArgumentSyntax<T> : ArgumentSyntax
    {
        internal ArgumentSyntax(bool required)
        {
            this.Required = required;
        }

        public sealed override bool Required { get; }
        public sealed override Type Type => typeof(T);
    }
}
