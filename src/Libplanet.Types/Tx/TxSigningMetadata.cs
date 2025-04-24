using System;
using Libplanet.Crypto;

namespace Libplanet.Types.Tx;

public sealed record class TxSigningMetadata(Address Signer, long Nonce)
{
    public TxSigningMetadata(PublicKey publicKey, long nonce)
        : this(publicKey.Address, nonce)
    {
    }

    public Address Signer { get; } = Signer;

    public long Nonce { get; } = ValidateNonce(Nonce);

    public override string ToString()
    {
        return nameof(TxMetadata) + " {\n" +
            $"  {nameof(Nonce)} = {Nonce},\n" +
            $"  {nameof(Signer)} = {Signer},\n" +
            "}";
    }

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
