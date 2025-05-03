using Libplanet.Types.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

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

    public static TxMetadata Create(Transaction metadata) => new()
    {
        Nonce = metadata.Nonce,
        GenesisHash = metadata.GenesisHash,
        UpdatedAddresses = metadata.UpdatedAddresses,
        Signer = metadata.Signer,
        Timestamp = metadata.Timestamp,
    };

    public bool Equals(TxMetadata? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);
}
