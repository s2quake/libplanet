using System.ComponentModel.DataAnnotations;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class TxInvoice : IEquatable<TxInvoice>, IValidatableObject
{
    [Property(0)]
    [NonDefault]
    public ImmutableArray<IValue> Actions { get; init; } = [];

    [Property(1)]
    public BlockHash GenesisHash { get; init; }

    [Property(2)]
    public ImmutableSortedSet<Address> UpdatedAddresses { get; init; } = [];

    [Property(3)]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [Property(4)]
    public FungibleAssetValue MaxGasPrice { get; init; }

    [Property(5)]
    [NonNegative]
    public long GasLimit { get; init; }

    public bool Equals(TxInvoice? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public UnsignedTx Combine(TxSigningMetadata signingMetadata)
        => new() { Invoice = this, SigningMetadata = signingMetadata };

    public Transaction Sign(PrivateKey privateKey, long nonce)
        => Combine(new TxSigningMetadata { Signer = privateKey.Address, Nonce = nonce }).Sign(privateKey);

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (GasLimit > 0 && MaxGasPrice == default)
        {
            yield return new ValidationResult(
                $"If {nameof(GasLimit)} is greater than 0, " +
                $"{nameof(MaxGasPrice)} must be set to a non-null value.",
                [nameof(MaxGasPrice)]);
        }

        if (MaxGasPrice != default && GasLimit == 0)
        {
            yield return new ValidationResult(
                $"If {nameof(MaxGasPrice)} is set to a non-null value, " +
                $"{nameof(GasLimit)} must be greater than 0.",
                [nameof(GasLimit)]);
        }
    }
}
