using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "TransactionMetadata")]
public sealed partial record class TransactionMetadata
{
    [Property(0)]
    [NonNegative]
    public long Nonce { get; init; }

    [Property(1)]
    [NotDefault]
    public required Address Signer { get; init; }

    [Property(2)]
    public BlockHash GenesisHash { get; init; }

    [Property(3)]
    public ImmutableArray<ActionBytecode> Actions { get; init; } = [];

    [Property(4)]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [Property(5)]
    public FungibleAssetValue? MaxGasPrice { get; init; }

    [Property(6)]
    [NonNegative]
    public long GasLimit { get; init; }

    public Transaction Sign(PrivateKey privateKey)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = true,
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
