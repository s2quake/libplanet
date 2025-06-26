using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.TestUtilities.Extensions;

public static class SignWithoutValidationExtensions
{
    public static Vote SignWithoutValidation(this VoteMetadata @this, PrivateKey privateKey)
        => @this.SignWithoutValidation(privateKey.AsSigner());

    public static Vote SignWithoutValidation(this VoteMetadata @this, ISigner signer)
    {
        var message = ModelSerializer.SerializeToBytes(@this);
        var signature = signer.Sign(message);
        return new Vote { Metadata = @this, Signature = [.. signature] };
    }
}
