using System.Collections;
using System.ComponentModel.DataAnnotations;
using static Libplanet.Serialization.DataAnnotations.NotDefaultAttribute;

namespace Libplanet.Serialization.DataAnnotations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is ICollection collection)
        {
            var valueType = value.GetType();
            if (valueType.IsValueType && IsDefault(value, valueType))
            {
                return false;
            }

            return collection.Count > 0;
        }

        if (value is IEnumerable enumerable)
        {
            var valueType = value.GetType();
            if (valueType.IsValueType && IsDefault(value, valueType))
            {
                return false;
            }

            var enumerator = enumerable.GetEnumerator();
            return enumerator.MoveNext();
        }

        return false;
    }
}
