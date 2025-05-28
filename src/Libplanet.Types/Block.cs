using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types;
using Libplanet.Types;

namespace Libplanet.Types;

[Model(Version = 1)]
public sealed partial record class Block : IComparable<Block>, IComparable
{
    [Property(0)]
    public required BlockHeader Header { get; init; }

    [Property(1)]
    public required BlockContent Content { get; init; }

    [Property(2)]
    public required ImmutableArray<byte> Signature { get; init; }

    public BlockHash BlockHash => BlockHash.Create(ModelSerializer.SerializeToBytes(this));

    public int Height => Header.Height;

    public int Version => Header.Version;

    public DateTimeOffset Timestamp => Header.Timestamp;

    public Address Proposer => Header.Proposer;

    public BlockHash PreviousHash => Header.PreviousHash;

    public BlockCommit PreviousCommit => Header.PreviousCommit;

    public HashDigest<SHA256> PreviousStateRootHash => Header.PreviousStateRootHash;

    public ImmutableSortedSet<Transaction> Transactions => Content.Transactions;

    public ImmutableSortedSet<EvidenceBase> Evidences => Content.Evidences;

    public override string ToString() => BlockHash.ToString();

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        Block other => CompareTo(other),
        _ => throw new ArgumentException($"Argument {nameof(obj)} is not ${nameof(Block)}.", nameof(obj)),
    };

    public int CompareTo(Block? other) => other switch
    {
        null => 1,
        _ => Height.CompareTo(other.Height),
    };
}
