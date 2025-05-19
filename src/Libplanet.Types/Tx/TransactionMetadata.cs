using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class TransactionMetadata : IEquatable<TransactionMetadata>
{
    [Property(0)]
    [NonNegative]
    public long Nonce { get; init; }

    [Property(1)]
    [NotDefault]
    public required Address Signer { get; init; }

    [Property(3)]
    public required ImmutableArray<ActionBytecode> Actions { get; init; }

    [Property(4)]
    public FungibleAssetValue? MaxGasPrice { get; init; }

    [Property(5)]
    [NonNegative]
    public long GasLimit { get; init; }

    [Property(6)]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [Property(7)]
    public BlockHash GenesisHash { get; init; }

    public bool Equals(TransactionMetadata? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);

    public Transaction Sign(PrivateKey privateKey)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = false,
        };
        var bytes = ModelSerializer.SerializeToBytes(this, options);
        var signature = privateKey.Sign(bytes).ToImmutableArray();

        return new Transaction
        {
            Metadata = this,
            Signature = signature,
        };
    }
}
