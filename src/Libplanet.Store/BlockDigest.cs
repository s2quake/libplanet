using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

[Model(Version = 1)]
public sealed record class BlockDigest : IEquatable<BlockDigest>
{
    [Property(0)]
    public required BlockHeader Header { get; init; }

    [Property(1)]
    public required BlockHash Hash { get; init; }

    [Property(2)]
    public required HashDigest<SHA256> StateRootHash { get; init; }

    [Property(3)]
    public required ImmutableSortedSet<TxId> TxIds { get; init; } = [];

    [Property(4)]
    public required ImmutableSortedSet<EvidenceId> EvidenceIds { get; init; } = [];

    public long Height => Header.Height;

    public Address Proposer => Header.Proposer;

    public BlockHash PreviousHash => Header.PreviousHash;

    public static BlockDigest Create(Block block) => new()
    {
        Header = block.Header,
        Hash = block.BlockHash,
        StateRootHash = block.StateRootHash,
        TxIds = [.. block.Content.Transactions.Select(tx => tx.Id)],
        EvidenceIds = [.. block.Content.Evidences.Select(ev => ev.Id)],
    };

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public bool Equals(BlockDigest? other) => ModelUtility.Equals(this, other);
}
