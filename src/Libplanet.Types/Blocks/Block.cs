using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Transactions;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed partial record class Block : IEquatable<Block>
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

    public override int GetHashCode() => ModelResolver.GetHashCode(this);

    public override string ToString() => BlockHash.ToString();

    public bool Equals(Block? other) => ModelResolver.Equals(this, other);
}
