using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Playground.CommandLineInterface.DataAnnotations
{
    [AttributeUsage(validOn: AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class SubCommandAttribute : ValidationAttribute
    {
        public override bool RequiresValidationContext => true;

        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (value == null) { return ValidationResult.Success; }

            if (value.GetType().IsValueType) { throw new InvalidOperationException($"the {typeof(SubCommandAttribute)} must only be used with reference type properties"); }

            var valueValidationContext = new ValidationContext(value, context.Items);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(value, valueValidationContext, results, validateAllProperties: true);

            if (isValid) { return ValidationResult.Success; }

            // todo name mapping?
            if (!results.Any()) { return new ValidationResult(context.MemberName + " is invalid", new[] { context.MemberName }); }

            return new ValidationResult(results[0].ErrorMessage, new[] { context.MemberName });
        }
    }
}
