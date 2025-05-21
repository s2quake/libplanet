using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Tx;

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
