using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface.DataAnnotations
{
    internal sealed class DataAnnotationsCommandLineParser
    {
        private readonly Type type;

        public DataAnnotationsCommandLineParser(Type type)
        {
            if (type == null) { throw new ArgumentNullException(nameof(type)); }
            if (!type.IsClass || type.IsAbstract) { throw new ArgumentException("must be a non-abstract class", nameof(type)); }

            this.type = type;

            var properties = TypeDescriptor.GetProperties(this.type);
            var arguments = properties.Cast<PropertyDescriptor>()
                .Select(ToArgumentAndPosition)
                .Where(kvp => kvp.Key != null)
                .OrderBy(kvp => kvp.Value ?? long.MaxValue)
                .Select(kvp => kvp.Key)
                .ToArray();

            // todo subcommands
            this.Syntax = CommandLineSyntax.Command(arguments);
        }

        public CommandSyntax Syntax { get; }

        private static KeyValuePair<ArgumentSyntax, int?> ToArgumentAndPosition(PropertyDescriptor property)
        {
            if (property.Attributes[typeof(NotMappedAttribute)] != null) { return default(KeyValuePair<ArgumentSyntax, int?>); }

            try
            {
                var description = (DescriptionAttribute)property.Attributes[typeof(DescriptionAttribute)];
                var displayName = (DisplayNameAttribute)property.Attributes[typeof(DisplayNameAttribute)];
                var display = (DisplayAttribute)property.Attributes[typeof(DisplayAttribute)];
                var isSubCommand = property.Attributes[typeof(SubCommandAttribute)] != null;

                if (display?.Description != null && description?.Description != null)
                {
                    throw new InvalidOperationException($"Description by both {typeof(DescriptionAttribute)} and {typeof(DisplayAttribute)}");
                }
                if (display?.Name != null && displayName?.DisplayName != null)
                {
                    throw new InvalidOperationException($"Name was specified by both {typeof(DisplayNameAttribute)} and {typeof(DisplayAttribute)}");
                }

                var specifiedName = displayName?.DisplayName ?? display?.GetName();
                var shortName = display?.GetShortName() != specifiedName ? display?.GetShortName() : null;
                var order = display?.GetOrder();
                
                if ((specifiedName != null || shortName != null) && order.HasValue)
                {
                    throw new InvalidOperationException("Both a positional order and a name were specified");
                }
                if (isSubCommand && order.HasValue)
                {
                    throw new InvalidOperationException("Subcommand may not specify an order");
                }

                var name = specifiedName ?? property.Name;

                if (isSubCommand) { throw new NotImplementedException(); }

                // todo shortname support

                if (order.HasValue)
                {
                    // todo generics
                    CommandLineSyntax.PositionalArgument<object>(
                        // todo cultureinfo cast
                        parser: (s, c) => property.Converter.ConvertFromString(null, (CultureInfo)c, s),
                        required: property.Attributes[typeof(RequiredAttribute)] != null
                    );
                }
                else if (property.PropertyType == typeof(bool))
                {
                    CommandLineSyntax.FlagArgument(name);
                }
                else
                {
                    //CommandLineSyntax.NamedArgument()
                }

                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to interpret property {property.ComponentType}.{property.Name} as an argument. See inner exception for details. To exclude the property, add the {typeof(NotMappedAttribute)}", ex);
            }
        }
    }
}
