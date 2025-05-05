using Xunit.Sdk;
using System.ComponentModel.DataAnnotations;

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
            throw new XunitException(
                $"Validation should have failed for {obj.GetType().Name} but it didn't.");
        }
        catch (ValidationException e)
        {
            return e;
        }
        catch (Exception e)
        {
            throw new XunitException("Validation failed", e);
        }
    }
}
