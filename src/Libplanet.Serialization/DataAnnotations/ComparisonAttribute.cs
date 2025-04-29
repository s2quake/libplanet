using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Libplanet.Serialization.DataAnnotations;

public abstract class ComparisonAttribute : ValidationAttribute
{
    private readonly Type? _targetType;
    private readonly string? _propertyName;
    private readonly object? _value;

    protected ComparisonAttribute(Type? targetType, string propertyName)
    {
        _targetType = targetType;
        _propertyName = propertyName;
    }

    protected ComparisonAttribute(object value)
    {
        _value = value;
    }

    public sealed override bool IsValid(object? value) => base.IsValid(value);

    public sealed override string FormatErrorMessage(string name) => base.FormatErrorMessage(name);

    protected abstract bool Compare(IComparable value, IComparable target);

    protected abstract string FormatErrorMessage(
        string name, IComparable value, IComparable target);

    protected sealed override ValidationResult? IsValid(
        object? value, ValidationContext validationContext)
    {
        try
        {
            if (value is not IComparable comparable)
            {
                return new ValidationResult(
                    $"{validationContext.DisplayName} must implement IComparable.");
            }

            var targetComparable = GetTargetComparable(comparable.GetType());
            if (!Compare(comparable, targetComparable))
            {
                return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
            }

            return ValidationResult.Success;
        }
        catch (Exception e)
        {
            return new ValidationResult(e.Message);
        }

        IComparable GetTargetComparable(Type propertyType)
        {
            if (_propertyName is { } propertyName)
            {
                return GetComparable(_targetType, propertyName, validationContext);
            }
            else if (_value is { } targetValue)
            {
                return GetComparable(propertyType, targetValue, validationContext);
            }
            else
            {
                throw new ValidationException("Target property or value not found.");
            }
        }
    }

    private static IComparable GetComparable(
        Type? targetType, string propertyName, ValidationContext validationContext)
    {
        if (propertyName == string.Empty)
        {
            throw new ValidationException("Property name cannot be empty.");
        }

        var objectType = targetType ?? validationContext.ObjectType;
        if (objectType == validationContext.ObjectType
            && propertyName == validationContext.MemberName)
        {
            throw new ValidationException("Property cannot reference itself.");
        }

        if (objectType.GetProperty(propertyName) is not { } propertyInfo)
        {
            throw new ValidationException($"Property {propertyName} not found.");
        }

        var propertyValue = propertyInfo.GetValue(validationContext.ObjectInstance);
        if (propertyValue is not IComparable targetComparable)
        {
            throw new ValidationException($"Property {propertyName} must implement IComparable.");
        }

        return targetComparable;
    }

    private static IComparable GetComparable(
        Type propertyType, object value, ValidationContext validationContext)
    {
        var displayName = validationContext.DisplayName;
        var targetComparable = value as IComparable;
        if (value is string @string)
        {
            if (propertyType == typeof(BigInteger))
            {
                targetComparable = BigInteger.Parse(@string, NumberStyles.Number);
            }
            else
            {
                var converter = TypeDescriptor.GetConverter(value.GetType());
                var targetValue = converter.ConvertFromInvariantString(@string);
                targetComparable = targetValue as IComparable;
            }
        }
        else if (value.GetType() != propertyType)
        {
            targetComparable = Convert.ChangeType(value, propertyType) as IComparable;
        }

        if (targetComparable is null)
        {
            throw new ValidationException(
                $"The value of {displayName} must implement IComparable.");
        }

        return targetComparable;
    }
}
