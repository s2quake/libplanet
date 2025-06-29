using System.ComponentModel.DataAnnotations;

namespace Libplanet.Types;

public sealed partial record class Block : IValidatableObject
{
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        yield break;
    }
}
