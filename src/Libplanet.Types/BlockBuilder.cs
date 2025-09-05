using System.Security.Cryptography;

namespace Libplanet.Types;

public sealed record class BlockBuilder
{
    public int Height { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public BlockHash PreviousBlockHash { get; init; }

    public BlockCommit PreviousBlockCommit { get; init; }

    public HashDigest<SHA256> PreviousStateRootHash { get; init; }

    public ImmutableSortedSet<Transaction> Transactions { get; init; } = [];

    public ImmutableSortedSet<EvidenceBase> Evidence { get; init; } = [];

    public Block Create(ISigner proposer)
    {
        var blockHeader = new BlockHeader
        {
            Version = BlockHeader.CurrentVersion,
            Height = Height,
            Timestamp = Timestamp == default ? DateTimeOffset.UtcNow : Timestamp,
            Proposer = proposer.Address,
            PreviousBlockHash = PreviousBlockHash,
            PreviousBlockCommit = PreviousBlockCommit,
            PreviousStateRootHash = PreviousStateRootHash,
        };
        var blockContent = new BlockContent
        {
            Transactions = Transactions,
            Evidence = Evidence,
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer);
    }
}
