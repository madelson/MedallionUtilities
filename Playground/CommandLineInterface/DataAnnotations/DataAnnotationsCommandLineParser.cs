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
        private readonly IReadOnlyDictionary<PropertyDescriptor, ArgumentSyntax> argumentMapping;
        private readonly IReadOnlyDictionary<PropertyDescriptor, DataAnnotationsCommandLineParser> subCommandMapping;

        public DataAnnotationsCommandLineParser(Type type)
            : this(type, subCommandName: null, subCommandDescription: null)
        {
        }

        private DataAnnotationsCommandLineParser(Type type, string subCommandName, string subCommandDescription)
        {
            if (type == null) { throw new ArgumentNullException(nameof(type)); }
            if (!type.IsClass || type.IsAbstract) { throw new ArgumentException("must be a non-abstract class", nameof(type)); }

            this.type = type;

            CommandSyntax syntax;
            CreateSyntax(type, subCommandName, subCommandDescription, out syntax, out this.argumentMapping, out this.subCommandMapping);
        }

        public CommandSyntax Syntax { get; }

        #region ---- Syntax Mapping ----
        private static void CreateSyntax(
            Type type, 
            string subCommandName,
            string subCommandDescription,
            out CommandSyntax syntax, 
            out IReadOnlyDictionary<PropertyDescriptor, ArgumentSyntax> argumentMapping, 
            out IReadOnlyDictionary<PropertyDescriptor, DataAnnotationsCommandLineParser> subCommandMapping)
        {
            var parsedProperties = TypeDescriptor.GetProperties(type)
                .Cast<PropertyDescriptor>()
                .Where(p => !p.IsReadOnly)
                .Select(p => new { prop = p, parseResult = ParseProperty(p) })
                .Where(t => t.parseResult != null)
                .Select(t => new { t.prop, parsed = t.parseResult.Item1, order = t.parseResult.Item2 })
                .ToArray();

            var arguments = parsedProperties.OrderBy(t => t.order ?? long.MaxValue)
                .Select(t => t.parsed)
                .OfType<ArgumentSyntax>();
            var subCommands = parsedProperties.Select(t => t.parsed)
                .OfType<DataAnnotationsCommandLineParser>()
                .Select(p => (SubCommandSyntax)p.Syntax);

            if (subCommandName != null)
            {
                syntax = CommandLineSyntax.SubCommand(
                    subCommandName,
                    subCommandDescription,
                    arguments,
                    subCommands
                );
            }
            else
            {
                var nameAndDescription = GetNameAndDescription(type);
                syntax = CommandLineSyntax.Command(
                    nameAndDescription.Item1,
                    nameAndDescription.Item2,
                    arguments,
                    subCommands
                );
            }

            argumentMapping = parsedProperties.Where(t => t.parsed is ArgumentSyntax)
                .ToDictionary(t => t.prop, t => (ArgumentSyntax)t.parsed);
            subCommandMapping = parsedProperties.Where(t => t.parsed is DataAnnotationsCommandLineParser)
                .ToDictionary(t => t.prop, t => (DataAnnotationsCommandLineParser)t.parsed);
        }

        private static Tuple<IEnumerable<ArgumentSyntax>, IEnumerable<SubCommandSyntax>> GetArgumentsAndSubCommands(Type type)
        {
            var properties = TypeDescriptor.GetProperties(type);
            var parsedProperties = properties.Cast<PropertyDescriptor>()
                .Where(p => !p.IsReadOnly)
                .Select(ParseProperty)
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

        private static Tuple<object, int?> ParseProperty(PropertyDescriptor property)
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
                    return new Tuple<object, int?>(
                        new DataAnnotationsCommandLineParser(property.PropertyType, subCommandName: name, subCommandDescription: specifiedDescription),
                        null
                    );
                }

                return (Tuple<object, int?>)typeof(DataAnnotationsCommandLineParser)
                    .GetMethod(nameof(ToArgumentSyntaxAndPosition), BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { name, shortName, description, order, required, property });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to interpret property {property.ComponentType}.{property.Name} as an argument. See inner exception for details. To exclude the property, add the {typeof(NotMappedAttribute)}", ex);
            }
        }

        private static Tuple<object, int?> ToArgumentSyntaxAndPosition<T>(
            string name,
            string shortName,
            string description,
            int? order,
            bool required,
            PropertyDescriptor property)
        {
            if (!required && !order.HasValue && typeof(T) == typeof(bool))
            {
                return Tuple.Create<object, int?>(
                    CommandLineSyntax.FlagArgument(name, shortName, description),
                    null
                );
            }

            var parser = property.Attributes[typeof(TypeConverterAttribute)] != null
                ? new Func<string, T>(s => (T)property.Converter.ConvertFromInvariantString(s))
                : null;

            return Tuple.Create<object, int?>(
                order.HasValue
                    ? CommandLineSyntax.PositionalArgument(name: name, description: description, required: required, parser: parser)
                    : CommandLineSyntax.NamedArgument(name: name, shortName: shortName, description: description, required: required, parser: parser),
                order
            );
        }
        #endregion

        #region ---- Object Creation ----
        public object CreateObject(ParsedCommandLine command)
        {
            if (command.Syntax != this.Syntax) { throw new ArgumentException("the given command's syntax must match that of the parser", nameof(command)); }

            return this.CreateObject(command.SubCommand, command.Arguments);
        }

        private object CreateObject(ParsedSubCommand subCommand, ParsedArgumentCollection arguments)
        {
            // create an instance
            object result;
            try { result = Activator.CreateInstance(this.type); }
            catch (Exception ex) { throw new InvalidOperationException($"Unable to construct command object of type {this.type}", ex); }

            // set all argument properties
            foreach (var kvp in this.argumentMapping)
            {
                object value;
                if (arguments.TryGetValue(kvp.Value, out value))
                {
                    kvp.Key.SetValue(result, value);
                }
            }

            // set the sub command property
            if (subCommand != null)
            {
                var subCommandProperty = this.subCommandMapping.Single(kvp => kvp.Value.Syntax == subCommand.Syntax);
                var value = subCommandProperty.Value.CreateObject(subCommand.SubCommand, subCommand.Arguments);
                subCommandProperty.Key.SetValue(result, value);
            }

            return result;
        }
        #endregion

        #region ---- Object Validation ----
        public void ValidateObject(object parsed)
        {
            if (parsed == null) { throw new ArgumentNullException(nameof(parsed)); }
            if (!this.type.IsInstanceOfType(parsed)) { throw new ArgumentException($"must be of type {this.type} but was {parsed.GetType()}", nameof(parsed)); }

            Validator.ValidateObject(parsed, new ValidationContext(parsed), validateAllProperties: true);
        }
        #endregion
    }
}
