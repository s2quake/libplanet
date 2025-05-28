using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types;
using Libplanet.Types;
using Libplanet.Types;
using Libplanet.Types;

namespace Libplanet.Data;

[Model(Version = 1)]
public sealed partial record class BlockDigest : IHasKey<BlockHash>
{
    [Property(0)]
    public required BlockHash BlockHash { get; init; }

    [Property(1)]
    public required BlockHeader Header { get; init; }

    [Property(2)]
    public required ImmutableArray<byte> Signature { get; init; }

    [Property(3)]
    public required ImmutableSortedSet<TxId> TxIds { get; init; } = [];

    [Property(4)]
    public required ImmutableSortedSet<EvidenceId> EvidenceIds { get; init; } = [];

    public int Height => Header.Height;

    public Address Proposer => Header.Proposer;

    public BlockHash PreviousHash => Header.PreviousHash;

    BlockHash IHasKey<BlockHash>.Key => BlockHash;

    public static explicit operator BlockDigest(Block block) => new()
    {
        Header = block.Header,
        Signature = block.Signature,
        TxIds = [.. block.Content.Transactions.Select(tx => tx.Id)],
        EvidenceIds = [.. block.Content.Evidences.Select(ev => ev.Id)],
        BlockHash = block.BlockHash,
    };

    public Block ToBlock(Func<TxId, Transaction> txGetter, Func<EvidenceId, EvidenceBase> evGetter) => new()
    {
        Header = Header,
        Signature = Signature,
        Content = new BlockContent
        {
            Transactions = [.. TxIds.Select(txId => txGetter(txId))],
            Evidences = [.. EvidenceIds.Select(evId => evGetter(evId))],
        },
    };
}
