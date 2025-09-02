using System.Collections;
using System.ComponentModel.DataAnnotations;
using static Libplanet.Serialization.TypeUtility;

namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var memberName = validationContext.DisplayName;
        var declaringType = validationContext.ObjectType;

        if (value is ICollection collection)
        {
            var valueType = value.GetType();
            if (valueType.IsValueType && IsDefault(value, valueType))
            {
                var message = $"The value specified in {GetType().Name} on " +
                              $"{declaringType.Name}.{memberName} must not be the default value.";
                return new ValidationResult(message, [memberName]);
            }

            if (collection.Count > 0)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(
                $"The collection specified in {GetType().Name} on " +
                $"{declaringType.Name}.{memberName} must not be empty.",
                [memberName]);
        }
        else if (value is IEnumerable enumerable)
        {
            var valueType = value.GetType();
            if (valueType.IsValueType && IsDefault(value, valueType))
            {
                var message = $"The value specified in {GetType().Name} on " +
                              $"{declaringType.Name}.{memberName} must not be the default value.";
                return new ValidationResult(message, [memberName]);
            }

            var enumerator = enumerable.GetEnumerator();
            if (enumerator.MoveNext())
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(
                $"The collection specified in {GetType().Name} on " +
                $"{declaringType.Name}.{memberName} must not be empty.",
                [memberName]);
        }
        else
        {
            var message = $"The value specified in {GetType().Name} on " +
                          $"{declaringType.Name}.{memberName} must be convertible to IEnumerable.";
            return new ValidationResult(message, [memberName]);
        }
    }
}
