using Libplanet.Types;
using System.Security.Cryptography;
using Libplanet.Data;

namespace Libplanet;

public sealed record class BlockBuilder
{
    public int Height { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public BlockHash PreviousHash { get; init; }

    public BlockCommit PreviousCommit { get; init; } = BlockCommit.Empty;

    public HashDigest<SHA256> PreviousStateRootHash { get; init; }

    public ImmutableSortedSet<Transaction> Transactions { get; init; } = [];

    public ImmutableSortedSet<EvidenceBase> Evidences { get; init; } = [];

    public Block Create(PrivateKey proposer)
    {
        var blockHeader = new BlockHeader
        {
            Version = BlockHeader.CurrentProtocolVersion,
            Height = Height,
            Timestamp = DateTimeOffset.UtcNow,
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

    public Block Create(PrivateKey proposer, Blockchain blockchain)
    {
        var tipInfo = blockchain.TipInfo;
        var builder = this with
        {
            Height = tipInfo.Height + 1,
            PreviousHash = tipInfo.BlockHash,
            PreviousCommit = tipInfo.BlockCommit,
            PreviousStateRootHash = tipInfo.StateRootHash,
        };
        return builder.Create(proposer);
    }

    public Block Create(PrivateKey proposer, Repository repository)
    {
        var builder = this with
        {
            Height = repository.Height + 1,
            PreviousHash = repository.BlockHash,
            PreviousCommit = repository.BlockCommit,
            PreviousStateRootHash = repository.StateRootHash,
        };
        return builder.Create(proposer);
    }
}
