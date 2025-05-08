using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public sealed record class CommittedActionContext
{
    public CommittedActionContext(IActionContext context, HashDigest<SHA256> previousState)
    {
        Signer = context.Signer;
        TxId = context.TxId;
        Proposer = context.Proposer;
        BlockHeight = context.BlockHeight;
        BlockProtocolVersion = context.BlockProtocolVersion;
        PreviousState = previousState;
        RandomSeed = context.RandomSeed;
    }

    public CommittedActionContext()
    {
    }

    public Address Signer { get; init; }

    public TxId TxId { get; init; }

    public Address Proposer { get; init; }

    public long BlockHeight { get; init; }

    public int BlockProtocolVersion { get; init; }

    public HashDigest<SHA256> PreviousState { get; init; }

    public int RandomSeed { get; init; }

    public IRandom GetRandom() => new Random(RandomSeed);
}
