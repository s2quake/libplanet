using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public sealed record class CommittedActionContext
{
    public CommittedActionContext(IActionContext context)
    {
        Signer = context.Signer;
        TxId = context.TxId;
        Miner = context.Miner;
        BlockHeight = context.BlockHeight;
        BlockProtocolVersion = context.BlockProtocolVersion;
        PreviousState = context.World.Trie.Hash;
        RandomSeed = context.RandomSeed;
        IsPolicyAction = context.IsPolicyAction;
    }

    public CommittedActionContext()
    {
    }

    public Address Signer { get; init; }

    public TxId? TxId { get; init; }

    public Address Miner { get; init; }

    public long BlockHeight { get; init; }

    public int BlockProtocolVersion { get; init; }

    public HashDigest<SHA256> PreviousState { get; init; }

    public int RandomSeed { get; init; }

    public bool IsPolicyAction { get; init; }

    public IRandom GetRandom() => new Random(RandomSeed);
}
