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
    public static BlockDigest Empty { get; } = new BlockDigest() { Header = BlockHeader.Empty, };

    [Property(0)]
    public required BlockHeader Header { get; init; }

    public BlockHashData Hash { get; init; } = BlockHashData.Empty;

    [Property(1)]
    public ImmutableSortedSet<TxId> TxIds { get; init; } = [];

    [Property(2)]
    public ImmutableSortedSet<EvidenceId> EvidenceIds { get; init; } = [];

    public long Height => Header.Height;

    public Address Proposer => Header.Proposer;

    public BlockHash PreviousHash => Header.PreviousHash;

    // public BlockHash Hash => Header.BlockHash;

    // public HashDigest<SHA256> StateRootHash => Header.StateRootHash;

    public static BlockDigest Create(Block block) => new()
    {
        Header = block.Header,
        TxIds = [.. block.Transactions.Select(tx => tx.Id)],
        EvidenceIds = [.. block.Evidence.Select(ev => ev.Id)],
    };

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public bool Equals(BlockDigest? other) => ModelUtility.Equals(this, other);
}
