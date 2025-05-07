using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class Block : IEquatable<Block>
{
    [Property(0)]
    public required BlockHeader Header { get; init; } = BlockHeader.Empty;

    [Property(1)]
    public required BlockContent Content { get; init; }

    [Property(2)]
    public required HashDigest<SHA256> StateRootHash { get; init; }

    [Property(3)]
    public required ImmutableArray<byte> Signature { get; init; }

    public BlockHash BlockHash => Header.DeriveBlockHash(StateRootHash, Signature);

    public long Height => Header.Height;

    public int ProtocolVersion => Header.ProtocolVersion;

    public DateTimeOffset Timestamp => Header.Timestamp;

    public Address Proposer => Header.Proposer;

    public BlockHash PreviousHash => Header.PreviousHash;

    public BlockCommit LastCommit => Header.LastCommit;

    public ImmutableSortedSet<Transaction> Transactions => Content.Transactions;

    public ImmutableSortedSet<EvidenceBase> Evidences => Content.Evidences;

    public static Block Create(
        BlockHeader header, ImmutableSortedSet<Transaction> transactions, ImmutableSortedSet<EvidenceBase> evidence)
    {
        throw new NotImplementedException();
        // return new Block
        // {
        //     Header = header,
        //     Content = new BlockContent
        //     {
        //         Transactions = transactions,
        //         Evidences = evidence,
        //     },
        // };
    }

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public override string ToString() => BlockHash.ToString();

    public bool Equals(Block? other) => ModelUtility.Equals(this, other);
}
