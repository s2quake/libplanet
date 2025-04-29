namespace Libplanet.Tests;

public static class TestValidator
{
    public static void Validate(object obj)
    {
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(
            instance: obj,
            validationContext: new System.ComponentModel.DataAnnotations.ValidationContext(obj),
            validateAllProperties: true);
    }
}
