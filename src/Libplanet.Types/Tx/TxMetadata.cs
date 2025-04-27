using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using static Libplanet.Types.BencodexUtility;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class TxMetadata : IEquatable<TxMetadata>
{
    [Property(0)]
    public long Nonce { get; init; }

    [Property(1)]
    public required Address Signer { get; init; }

    [Property(2)]
    public ImmutableSortedSet<Address> UpdatedAddresses { get; init; } = [];

    [Property(3)]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [Property(4)]
    public BlockHash? GenesisHash { get; init; }

    public static TxMetadata Create(IValue value)
    {
        if (value is not List list)
        {
            throw new ArgumentException("Serialized value must be a list.", nameof(value));
        }

        return new TxMetadata
        {
            Nonce = ToInt64(list, 0),
            Signer = ToAddress(list, 1),
            UpdatedAddresses = [.. ToObjects(list, 2, ModelSerializer.Deserialize<Address>)],
            Timestamp = ToDateTimeOffset(list, 3),
            GenesisHash = ToBlockHashOrDefault(list, 4),
        };
    }

    public static TxMetadata Create(Transaction metadata)
    {
        return new TxMetadata
        {
            Nonce = metadata.Nonce,
            GenesisHash = metadata.GenesisHash,
            UpdatedAddresses = metadata.UpdatedAddresses,
            Signer = metadata.Signer,
            Timestamp = metadata.Timestamp,
        };
    }

    public bool Equals(TxMetadata? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public IValue ToBencodex()
    {
        return new List(
            ToValue(Nonce),
            ToValue(Signer),
            ToValue([.. UpdatedAddresses], item => ModelSerializer.Serialize(item)),
            ToValue(Timestamp),
            ToValue(GenesisHash));
    }
}
