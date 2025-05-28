using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;

namespace Libplanet.Types;

public sealed partial record class Transaction : IValidatableObject
{
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        var message = ModelSerializer.SerializeToBytes(Metadata);
        if (!Signer.Verify([.. message], Signature))
        {
            yield return new ValidationResult("Transaction signature is invalid.", [nameof(Signature)]);
        }
    }
}
