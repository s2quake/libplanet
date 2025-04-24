using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class TxSigningMetadata(Address Signer, long Nonce)
{
    public TxSigningMetadata(PublicKey publicKey, long nonce)
        : this(publicKey.Address, nonce)
    {
    }

    [Property(0)]
    public Address Signer { get; } = Signer;

    [Property(1)]
    public long Nonce { get; } = ValidateNonce(Nonce);

    private static long ValidateNonce(long nonce)
    {
        if (nonce < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nonce),
                $"The nonce must be greater than or equal to 0, but {nonce} was given.");
        }

        return nonce;
    }
}
