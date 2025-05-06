using System.ComponentModel.DataAnnotations;
using Xunit.Sdk;

namespace Libplanet.Tests;

public static class TestValidator
{
    public static ValidationException Throws(object obj)
    {
        try
        {
            Validator.ValidateObject(
                instance: obj,
                validationContext: new ValidationContext(obj),
                validateAllProperties: true);
            throw new XunitException($"Validation should have failed for {obj.GetType().Name} but it didn't.");
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
                throw new XunitException(
                    $"Validation should have failed for {obj.GetType().Name}.{propertyName} but it didn't.");
            }

            return e;
        }
    }
}
