using System.ComponentModel.DataAnnotations;

namespace Libplanet.Types;

public static class ValidationUtility
{
    public static void Validate(object obj) => Validator.ValidateObject(instance: obj, new(obj), true);

    public static bool TryValidate(object obj) => Validator.TryValidateObject(obj, new(obj), null, true);

    public static bool TryValidate(object obj, ICollection<ValidationResult>? validationResults)
        => Validator.TryValidateObject(obj, new(obj), validationResults, true);
}
