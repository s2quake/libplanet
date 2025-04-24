using System;
using System.Collections.Immutable;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using static Libplanet.Types.BencodexUtility;

namespace Libplanet.Types.Tx;

public sealed record class TxMetadata : IEquatable<TxMetadata>
{
    public long Nonce { get; init; }

    public required Address Signer { get; init; }

    public ImmutableSortedSet<Address> UpdatedAddresses { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

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
            UpdatedAddresses = [.. ToObjects(list, 2, Address.Create)],
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

    public bool Equals(TxMetadata? other)
    {
        if (other is { } o)
        {
            return Nonce == other.Nonce &&
                   Signer.Equals(other.Signer) &&
                   UpdatedAddresses.SetEquals(other.UpdatedAddresses) &&
                   Timestamp.Equals(other.Timestamp) &&
                   Equals(GenesisHash, other.GenesisHash);
        }

        return false;
    }

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Nonce);
        hash.Add(Signer);
        foreach (var updatedAddress in UpdatedAddresses)
        {
            hash.Add(updatedAddress);
        }

        hash.Add(Timestamp);
        hash.Add(GenesisHash);

        return hash.ToHashCode();
    }

    public IValue ToBencodex()
    {
        return new List(
            ToValue(Nonce),
            ToValue(Signer),
            ToValue([.. UpdatedAddresses], item => item.ToBencodex()),
            ToValue(Timestamp),
            ToValue(GenesisHash));
    }
}
