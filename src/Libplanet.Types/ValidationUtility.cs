using System.ComponentModel.DataAnnotations;

namespace Libplanet.Types;

public static class ValidationUtility
{
    public static void Validate(object obj)
        => System.ComponentModel.DataAnnotations.Validator.ValidateObject(instance: obj, new(obj), true);

    public static void Validate(object obj, IDictionary<object, object?> items)
        => System.ComponentModel.DataAnnotations.Validator.ValidateObject(instance: obj, new(obj, items), true);

    public static T ValidateAndReturn<T>(T obj)
        where T : notnull
    {
        Validate(obj);
        return obj;
    }

    public static T ValidateAndReturn<T>(T obj, IDictionary<object, object?> items)
        where T : notnull
    {
        Validate(obj, items);
        return obj;
    }

    public static bool TryValidate(object obj)
        => System.ComponentModel.DataAnnotations.Validator.TryValidateObject(obj, new(obj), null, true);

    public static bool TryValidate(object obj, ICollection<ValidationResult>? validationResults)
        => System.ComponentModel.DataAnnotations.Validator.TryValidateObject(obj, new(obj), validationResults, true);

    public static ValidationException Throws(object obj)
    {
        try
        {
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(
                instance: obj,
                validationContext: new ValidationContext(obj),
                validateAllProperties: true);
            throw new ArgumentException(
                $"Validation should have failed for {obj.GetType().Name} but it didn't.", nameof(obj));
        }
        catch (ValidationException e)
        {
            return e;
        }
    }

    public static ValidationException Throws(object obj, string propertyName)
    {
        try
        {
            return Throws(obj);
        }
        catch (ValidationException e)
        {
            if (!e.ValidationResult.MemberNames.Contains(propertyName))
            {
                throw new ArgumentException(
                    $"Validation should have failed for {obj.GetType().Name}.{propertyName} but it didn't.",
                    nameof(propertyName));
            }

            return e;
        }
    }
}
