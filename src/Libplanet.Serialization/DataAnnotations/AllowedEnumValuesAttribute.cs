using System.ComponentModel.DataAnnotations;

namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AllowedEnumValuesAttribute(params object[] enumValues) : ValidationAttribute
{
    public object[] EnumValues { get; } = enumValues;

    public Type? EnumType { get; init; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var objectType = validationContext.ObjectType;

        foreach (var enumValue in EnumValues)
        {
            if (enumValue.GetType() != EnumType)
            {
                return new ValidationResult("The enum values specified in the attribute must be of type {EnumType}.");
            }

            if (!enumValue.GetType().IsEnum)
            {
                return new ValidationResult("The enum values specified in the attribute must be of an enum type.");
            }
        }

        if (!objectType.IsEnum)
        {
            return new ValidationResult("The property must be of an enum type.");
        }

        if (value is null)
        {
            return new ValidationResult("The value cannot be null.");
        }

        if (!EnumValues.Contains(value))
        {
            return new ValidationResult(
                $"The value '{value}' cannot be used. It is not one of the allowed enum values: " +
                $"{string.Join(", ", EnumValues)}.");
        }

        return ValidationResult.Success;
    }
}
