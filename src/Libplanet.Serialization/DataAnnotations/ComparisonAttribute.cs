using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public abstract class ComparisonAttribute : ValidationAttribute
{
    private readonly (Type?, string)? _targetAndPropertyName;
    private readonly object? _value;
    private readonly (string, Type)? _textValueAndType;

    protected ComparisonAttribute(Type? targetType, string propertyName)
        => _targetAndPropertyName = (targetType, propertyName);

    protected ComparisonAttribute(object value) => _value = value;

    protected ComparisonAttribute(string textValue, Type valueType)
        => _textValueAndType = (textValue, valueType);

    protected abstract bool Compare(IComparable value, IComparable target);

    protected abstract string FormatErrorMessage(
        string memberName, Type declaringType, IComparable value, IComparable target);

    protected sealed override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var memberName = validationContext.DisplayName;
        var declaringType = validationContext.ObjectType;
        try
        {
            if (value is not IComparable comparable)
            {
                return new ValidationResult($"{memberName} must implement IComparable.");
            }

            var targetComparable = GetTargetComparable(value.GetType(), validationContext);
            if (!Compare(comparable, targetComparable, validationContext))
            {
                var message = FormatErrorMessage(memberName, declaringType, comparable, targetComparable);
                return new ValidationResult(message, [memberName]);
            }

            return ValidationResult.Success;
        }
        catch (Exception e)
        {
            return new ValidationResult(e.Message, [memberName]);
        }
    }

    protected virtual IComparable GetTargetComparable(Type valueType, ValidationContext validationContext)
    {
        var memberName = validationContext.DisplayName;
        var declaringType = validationContext.ObjectType;

        if (_targetAndPropertyName is { } targetAndPropertyName)
        {
            var (targetType, propertyName) = targetAndPropertyName;
            return GetComparable(targetType, propertyName, validationContext);
        }
        else if (_textValueAndType is { } textValueAndType)
        {
            var (textValue, textType) = textValueAndType;
            if (!typeof(IComparable).IsAssignableFrom(textType))
            {
                var message = $"The type {valueType.Name} specified in {GetType().Name} on " +
                              $"{declaringType.Name}.{memberName} must implement IComparable.";
                throw new InvalidOperationException(message);
            }

            var converted = ConvertFrom(textValue, valueType);
            if (converted is null)
            {
                var message = $"The value '{textValue}' specified in {GetType().Name} on " +
                              $"{declaringType.Name}.{memberName} cannot be converted to {valueType.Name}.";
                throw new InvalidOperationException(message);
            }

            return converted;
        }
        else if (_value is IComparable targetComparable)
        {
            return targetComparable;
        }
        else
        {
            var message = $"The value specified in {GetType().Name} on " +
                          $"{declaringType.Name}.{memberName} cannot be null or must be convertible to IComparable.";
            throw new InvalidOperationException(message);
        }
    }

    private bool Compare(IComparable value, IComparable target, ValidationContext validationContext)
    {
        try
        {
            return Compare(value, target);
        }
        catch (Exception e)
        {
            var memberName = validationContext.DisplayName;
            var declaringType = validationContext.ObjectType;
            var message = $"An exception occurred when comparing the property {memberName} of " +
                          $"{declaringType.Name} with the target value {target}({target.GetType().Name}). " +
                          $"Current value: {value}({value.GetType().Name}).";
            throw new InvalidOperationException(message, e);
        }
    }

    private IComparable GetComparable(Type? targetType, string propertyName, ValidationContext validationContext)
    {
        var memberName = validationContext.DisplayName;
        var declaringType = validationContext.ObjectType;

        if (propertyName == string.Empty)
        {
            var message = $"The property specified in {GetType().Name} on {declaringType.Name}.{memberName} " +
                          $"cannot be empty.";
            throw new ArgumentException(message, nameof(propertyName));
        }

        var objectType = targetType ?? validationContext.ObjectType;
        if (objectType == validationContext.ObjectType
            && propertyName == validationContext.MemberName)
        {
            var message = $"The property {propertyName} specified in {GetType().Name} on " +
                          $"{declaringType.Name}.{memberName} cannot reference itself.";
            throw new ArgumentException(message, nameof(propertyName));
        }

        if (objectType.GetProperty(propertyName) is not { } propertyInfo)
        {
            var targetPropertyName = targetType is null ? propertyName : $"{targetType.Name}.{propertyName}";
            var message = $"The property {targetPropertyName} specified in {GetType().Name} on " +
                          $"{declaringType.Name}.{memberName} cannot be found.";
            throw new ArgumentException(message, nameof(propertyName));
        }

        var propertyValue = propertyInfo.GetValue(validationContext.ObjectInstance);
        if (propertyValue is not IComparable targetComparable)
        {
            var targetPropertyName = targetType is null ? propertyName : $"{targetType.Name}.{propertyName}";
            var message = $"The property {targetPropertyName} specified in {GetType().Name} on " +
                          $"{declaringType.Name}.{memberName} must implement IComparable.";
            throw new ArgumentException(message, nameof(propertyName));
        }

        return targetComparable;
    }

    private static IComparable? ConvertFrom(string textValue, Type valueType)
    {
        // Since the BigInteger type is frequently used in types such as Currency in blockchain,
        // I have exceptionally added the following code.
        if (valueType == typeof(BigInteger))
        {
            if (BigInteger.TryParse(textValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }

            return null;
        }

        var converter = TypeDescriptor.GetConverter(valueType);
        return converter.ConvertFromInvariantString(textValue) as IComparable;
    }
}
