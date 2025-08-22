using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;

namespace Libplanet.Types;

public static class ValidationUtility
{
    public static void Validate(object obj)
    {
        var type = obj.GetType();
        if (!type.IsValueType || !TypeUtility.IsDefault(obj, type))
        {
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(instance: obj, new(obj), true);
        }
    }

    public static void Validate(object obj, IDictionary<object, object?> items)
    {
        var type = obj.GetType();
        if (!type.IsValueType || !TypeUtility.IsDefault(obj, type))
        {
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(instance: obj, new(obj, items), true);
        }
    }

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
}
