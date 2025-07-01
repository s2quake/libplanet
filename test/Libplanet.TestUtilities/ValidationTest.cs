using System.ComponentModel.DataAnnotations;

namespace Libplanet.TestUtilities;

public static class ValidationTest
{
    [AssertionMethod]
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
}
