using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class TxSigningMetadata
{
    [Property(0)]
    public Address Signer { get; init; }

    [Property(1)]
    [NonNegative]
    public long Nonce { get; init; }

    public static TxSigningMetadata Create(PublicKey publicKey, long nonce) => new()
    {
        Signer = publicKey.Address,
        Nonce = nonce,
    };
}
