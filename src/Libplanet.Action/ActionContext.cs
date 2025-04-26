using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

internal sealed record class ActionContext : IActionContext
{
    public Address Signer { get; init; }

    // Address IActionSigner
    //     => Signer ?? throw new InvalidOperationException("Signer cannot be used in BlockAction");

    public TxId? TxId { get; init; }

    public Address Miner { get; init; }

    public long BlockHeight { get; init; }

    public int BlockProtocolVersion { get; init; }

    public BlockCommit? LastCommit { get; init; }

    public required IWorld World { get; init; }

    public int RandomSeed { get; init; }

    public bool IsPolicyAction { get; init; }

    public FungibleAssetValue? MaxGasPrice { get; init; }

    public ImmutableSortedSet<Transaction> Txs { get; init; } = [];

    public ImmutableSortedSet<EvidenceBase> Evidence { get; init; } = [];

    public IRandom GetRandom() => new Random(RandomSeed);

    public CommittedActionContext ToCommittedActionContext()
        => new()
        {
            Signer = Signer,
            TxId = TxId,
            Miner = Miner,
            BlockHeight = BlockHeight,
            BlockProtocolVersion = BlockProtocolVersion,
            PreviousState = World.Trie.IsCommitted
                ? World.Trie.Hash
                : throw new ArgumentException("Trie is not recorded"),
            RandomSeed = RandomSeed,
            IsPolicyAction = IsPolicyAction,
        };
}
