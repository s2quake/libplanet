using System.ComponentModel.DataAnnotations;

namespace Libplanet.TestUtilities;

public static class ValidationTest
{
    [AssertionMethod]
    public static ValidationException Throws(object obj)
    {
        try
        {
            Validator.ValidateObject(
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

    [AssertionMethod]
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

    [AssertionMethod]
    public static void ThrowsMany(object obj, string[] propertyNames)
    {
        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(obj);
        if (Validator.TryValidateObject(obj, validationContext, results, true))
        {
            throw new ArgumentException(
                $"Validation should have failed for {obj.GetType().Name} but it didn't.",
                nameof(obj));
        }

        foreach (var propertyName in propertyNames)
        {
            if (!results.Any(r => r.MemberNames.Contains(propertyName)))
            {
                throw new ArgumentException(
                    $"Validation should have failed for {obj.GetType().Name}.{propertyName} but it didn't.",
                    nameof(propertyNames));
            }
        }

        foreach (var result in results)
        {
            if (result.MemberNames.Any(n => !propertyNames.Contains(n)))
            {
                var memberNames = string.Join(", ", result.MemberNames);
                throw new ArgumentException(
                    $"Validation should not have failed for {obj.GetType().Name}.{memberNames} but it did.",
                    nameof(propertyNames));
            }
        }
    }

    [AssertionMethod]
    public static void DoseNotThrow(object obj)
    {
        Validator.ValidateObject(
            instance: obj,
            validationContext: new ValidationContext(obj),
            validateAllProperties: true);
    }
}
