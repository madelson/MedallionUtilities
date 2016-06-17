using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Reflection;
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

            var argumentsAndSubCommands = GetArgumentsAndSubCommands(this.type);
            var nameAndDescription = GetNameAndDescription(this.type);
            this.Syntax = CommandLineSyntax.Command(
                nameAndDescription.Item1,
                nameAndDescription.Item2,
                argumentsAndSubCommands.Item1,
                argumentsAndSubCommands.Item2
            );
        }

        public CommandSyntax Syntax { get; }

        private static Tuple<IEnumerable<ArgumentSyntax>, IEnumerable<SubCommandSyntax>> GetArgumentsAndSubCommands(Type type)
        {
            var properties = TypeDescriptor.GetProperties(type);
            var parsedProperties = properties.Cast<PropertyDescriptor>()
                .Select(ToSyntaxAndPosition)
                .Where(t => t != null)
                .ToArray();
            return Tuple.Create(
                parsedProperties.OrderBy(t => t.Item2 ?? long.MaxValue).Select(t => t.Item1).OfType<ArgumentSyntax>(),
                parsedProperties.Select(t => t.Item1).OfType<SubCommandSyntax>()
            );
        }

        private static Tuple<string, string> GetNameAndDescription(Type type)
        {
            var attributes = TypeDescriptor.GetAttributes(type);
            return Tuple.Create(
                ((DisplayNameAttribute)attributes[typeof(DisplayNameAttribute)])?.DisplayName ?? type.Name,
                ((DescriptionAttribute)attributes[typeof(DescriptionAttribute)])?.Description
            );
        }

        private static Tuple<CommandLineElementSyntax, int?> ToSyntaxAndPosition(PropertyDescriptor property)
        {
            if (property.Attributes[typeof(NotMappedAttribute)] != null) { return null; }

            try
            {
                var isSubCommand = property.Attributes[typeof(SubCommandAttribute)] != null;
                var required = property.Attributes[typeof(RequiredAttribute)] != null;

                var description = (DescriptionAttribute)property.Attributes[typeof(DescriptionAttribute)];
                var displayName = (DisplayNameAttribute)property.Attributes[typeof(DisplayNameAttribute)];
                var display = (DisplayAttribute)property.Attributes[typeof(DisplayAttribute)];

                if (display?.Description != null && description?.Description != null)
                {
                    throw new InvalidOperationException($"Description by both {typeof(DescriptionAttribute)} and {typeof(DisplayAttribute)}");
                }
                if (display?.Name != null && displayName?.DisplayName != null)
                {
                    throw new InvalidOperationException($"Name was specified by both {typeof(DisplayNameAttribute)} and {typeof(DisplayAttribute)}");
                }

                var specifiedName = displayName?.DisplayName ?? display?.GetName();
                var specifiedDescription = description?.Description ?? display?.GetDescription();
                var shortName = display?.GetShortName() != specifiedName ? display?.GetShortName() : null;
                var order = display?.GetOrder();
                
                if ((specifiedName != null || shortName != null) && order.HasValue)
                {
                    throw new InvalidOperationException("Both a positional order and a name were specified");
                }
                if (isSubCommand && order.HasValue)
                {
                    throw new InvalidOperationException("Sub-command may not specify an order");
                }
                if (isSubCommand && shortName != null)
                {
                    throw new InvalidOperationException("Sub-command may not specify a short name");
                }

                var name = specifiedName ?? property.Name;

                if (isSubCommand)
                {
                    var argumentsAndSubCommands = GetArgumentsAndSubCommands(property.PropertyType);
                    var syntax = CommandLineSyntax.SubCommand(
                        name,
                        specifiedDescription,
                        argumentsAndSubCommands.Item1,
                        argumentsAndSubCommands.Item2
                    );
                    return new Tuple<CommandLineElementSyntax, int?>(syntax, null);
                }

                return (Tuple<CommandLineElementSyntax, int?>)typeof(DataAnnotationsCommandLineParser)
                    .GetMethod(nameof(ToArgumentSyntaxAndPosition), BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { name, shortName, description, order, required, property });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to interpret property {property.ComponentType}.{property.Name} as an argument. See inner exception for details. To exclude the property, add the {typeof(NotMappedAttribute)}", ex);
            }
        }

        private static Tuple<CommandLineElementSyntax, int?> ToArgumentSyntaxAndPosition<T>(
            string name,
            string shortName,
            string description,
            int? order,
            bool required,
            PropertyDescriptor property)
        {
            if (!required && !order.HasValue && typeof(T) == typeof(bool))
            {
                return Tuple.Create<CommandLineElementSyntax, int?>(
                    CommandLineSyntax.FlagArgument(name, shortName, description),
                    null
                );
            }

            var parser = property.Attributes[typeof(TypeConverterAttribute)] != null
                ? new Func<string, T>(s => (T)property.Converter.ConvertFromInvariantString(s))
                : null;

            return Tuple.Create<CommandLineElementSyntax, int?>(
                order.HasValue
                    ? CommandLineSyntax.PositionalArgument(name: name, description: description, required: required, parser: parser)
                    : CommandLineSyntax.NamedArgument(name: name, shortName: shortName, description: description, required: required, parser: parser),
                order
            );
        }
    }
}
