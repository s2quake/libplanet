using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class Block : IEquatable<Block>
{
    // public const int CurrentProtocolVersion = BlockMetadata.CurrentProtocolVersion;

    [Property(0)]
    public required BlockHeader Header { get; init; } = BlockHeader.Empty;

    [Property(1)]
    public required BlockContent Content { get; init; }

    public int ProtocolVersion => Header.ProtocolVersion;

    public BlockHash Hash => Header.BlockHash;

    public ImmutableArray<byte> Signature => Header.Signature;

    public HashDigest<SHA256> RawHash => Header.RawHash;

    public HashDigest<SHA256> StateRootHash => Header.StateRootHash;

    public long Height => Header.Height;

    public Address Proposer => Header.Proposer;

    public BlockHash PreviousHash => Header.PreviousHash;

    public DateTimeOffset Timestamp => Header.Timestamp;

    public HashDigest<SHA256>? TxHash => Header.TxHash;

    public BlockCommit LastCommit => Header.LastCommit;

    public HashDigest<SHA256>? EvidenceHash => Header.EvidenceHash;

    public ImmutableSortedSet<EvidenceBase> Evidence => Content.Evidences;

    public ImmutableSortedSet<Transaction> Transactions => Content.Transactions;

    public static Block Create(
        BlockHeader header, ImmutableSortedSet<Transaction> transactions, ImmutableSortedSet<EvidenceBase> evidence)
    {
        return new Block
        {
            Header = header,
            Content = new BlockContent
            {
                Transactions = transactions,
                Evidences = evidence,
            },
        };
    }

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public override string ToString() => Hash.ToString();

    public bool Equals(Block? other) => ModelUtility.Equals(this, other);
}
