using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    internal sealed class EnumDescriptor
    {
        private readonly Type type;

        private EnumDescriptor(Type type)
        {
            if (type == null) { throw new ArgumentNullException(nameof(type)); }
            if (!type.IsEnum) { throw new ArgumentException("must be an enum type", nameof(type)); }

            this.type = type;
            this.IsFlags = type.IsDefined(typeof(FlagsAttribute), inherit: false);

            var values = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == type)
                .Select(ToValueDescriptor)
                .ToArray();
            if (values.GroupBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            {
                throw new InvalidOperationException($"Multiple values of {type} have the same display name");
            }
            this.Values = values;
        }

        public static EnumDescriptor ForType(Type type) => new EnumDescriptor(type);

        public bool IsFlags { get; }
        public IReadOnlyList<EnumValueDescriptor> Values { get; }

        public object Parse(string text)
        {
            if (text == null) { throw new ArgumentNullException(nameof(text)); }

            return this.IsFlags
                ? this.CombineFlags(text.Split('|').Select(this.InternalParse))
                : this.InternalParse(text);
        }

        private object InternalParse(string text)
        {
            var value = this.Values.FirstOrDefault(v => StringComparer.OrdinalIgnoreCase.Equals(text, v.DisplayName));
            if (value == null)
            {
                throw new ArgumentException($"unrecognized value '{text}'. Expected one of {string.Join(", ", this.Values.Select(v => $"'{v.DisplayName}"))}");
            }

            return value.Value;
        }

        private object CombineFlags(IEnumerable<object> values)
        {
            var underlyingType = Enum.GetUnderlyingType(this.type);
            var combined = IsSigned(underlyingType)
                ? values.Select(Convert.ToInt64).Aggregate(0L, (a, b) => a | b)
                : (object)values.Select(Convert.ToUInt64).Aggregate(0UL, (a, b) => a | b);

            return Enum.ToObject(this.type, combined);
        }

        private static bool IsSigned(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return false;

                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return true;

                default:
                    throw new InvalidOperationException($"expected an integral type. Found {type}");
            }
        }

        private static EnumValueDescriptor ToValueDescriptor(FieldInfo enumField)
        {
            var displayNameAttribute = enumField.GetCustomAttribute<DisplayNameAttribute>();
            var descriptionAttribute = enumField.GetCustomAttribute<DescriptionAttribute>();
            var displayAttribute = enumField.GetCustomAttribute<DisplayAttribute>();

            if (displayNameAttribute?.DisplayName != null && displayAttribute?.Name != null)
            {
                throw new InvalidOperationException($"enum field {enumField} specifies a display name multiple times");
            }
            if (descriptionAttribute?.Description != null && displayAttribute?.Description != null)
            {
                throw new InvalidOperationException($"enum field {enumField} specifies a description multiple times");
            }

            return new EnumValueDescriptor(
                enumField.GetValue(null),
                displayNameAttribute?.DisplayName ?? displayAttribute?.GetName() ?? enumField.Name,
                descriptionAttribute?.Description ?? displayAttribute?.GetDescription()
            );
        }
    }

    internal sealed class EnumValueDescriptor
    {
        public EnumValueDescriptor(object value, string displayName, string description)
        {
            if (value == null) { throw new ArgumentNullException(nameof(value)); }
            if (displayName == null) { throw new ArgumentNullException(nameof(displayName)); }

            this.Value = value;
            this.DisplayName = displayName;
            this.Description = description;
        }

        public object Value { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }
}
