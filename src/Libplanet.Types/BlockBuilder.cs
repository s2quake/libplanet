using System.Security.Cryptography;

namespace Libplanet.Types;

public sealed record class BlockBuilder
{
    public int Height { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public BlockHash PreviousHash { get; init; }

    public BlockCommit PreviousCommit { get; init; } = BlockCommit.Empty;

    public HashDigest<SHA256> PreviousStateRootHash { get; init; }

    public ImmutableSortedSet<Transaction> Transactions { get; init; } = [];

    public ImmutableSortedSet<EvidenceBase> Evidences { get; init; } = [];

    public Block Create(ISigner proposer)
    {
        var blockHeader = new BlockHeader
        {
            BlockVersion = BlockHeader.CurrentProtocolVersion,
            Height = Height,
            Timestamp = Timestamp == default ? DateTimeOffset.UtcNow : Timestamp,
            Proposer = proposer.Address,
            PreviousHash = PreviousHash,
            PreviousCommit = PreviousCommit,
            PreviousStateRootHash = PreviousStateRootHash,
        };
        var blockContent = new BlockContent
        {
            Transactions = Transactions,
            Evidences = Evidences,
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer);
    }
}
