using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed record class ProposalBuilder
{
    public required Block Block { get; init; }

    public HashDigest<SHA256> StateRootHash { get; init; }

    public int Round { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public int ValidRound { get; init; } = -1;

    public Proposal Create(ISigner signer)
    {
        var metadata = new ProposalMetadata
        {
            BlockHash = Block.BlockHash,
            StateRootHash = StateRootHash,
            Height = Block.Height,
            Round = Round,
            Timestamp = Timestamp == default ? DateTimeOffset.UtcNow : Timestamp,
            Proposer = Block.Proposer,
            ValidRound = ValidRound,
        };
        return metadata.Sign(signer, Block);
    }
}
