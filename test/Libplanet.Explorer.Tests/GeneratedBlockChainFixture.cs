using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Store;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Explorer.Tests;

public class GeneratedBlockChainFixture
{
    public static Currency TestCurrency => Currency.Create("TEST", 0);

    public BlockChain Chain { get; }

    public ImmutableArray<PrivateKey> PrivateKeys { get; }

    public int MaxTxCount { get; }

    public ImmutableDictionary<Address, ImmutableArray<Block>>
        MinedBlocks { get; private set; }

    public ImmutableDictionary<Address, ImmutableArray<Transaction>>
        SignedTxs { get; private set; }

    private System.Random Random { get; }

    public GeneratedBlockChainFixture(
        int seed,
        int blockCount = 20,
        int maxTxCount = 20,
        int privateKeyCount = 10,
        ImmutableArray<ImmutableArray<ImmutableArray<SimpleAction>>>?
            txActionsForSuffixBlocks = null)
    {
        txActionsForSuffixBlocks ??=
            ImmutableArray<ImmutableArray<ImmutableArray<SimpleAction>>>.Empty;

        var store = new MemoryStore();
        var stateStore = new TrieStateStore();

        Random = new System.Random(seed);
        MaxTxCount = maxTxCount;
        PrivateKeys = Enumerable
            .Range(0, privateKeyCount)
            .Select(_ => new PrivateKey())
            .ToImmutableArray();
        MinedBlocks = PrivateKeys
            .ToImmutableDictionary(
                key => key.Address,
                key => ImmutableArray<Block>.Empty);
        SignedTxs = PrivateKeys
            .ToImmutableDictionary(
                key => key.Address,
                key => ImmutableArray<Transaction>.Empty);

        var privateKey = new PrivateKey();
        var policy = new BlockPolicy
        {
            BlockInterval = TimeSpan.FromMilliseconds(1),
            MaxTransactionsPerBlock = int.MaxValue,
            MaxTransactionsBytes = long.MaxValue,
        };
        var actionEvaluator = new ActionEvaluator(
            stateStore,
            policy.PolicyActions);
        Block genesisBlock = BlockChain.ProposeGenesisBlock(
            proposer: new PrivateKey(),
            transactions: PrivateKeys
                .OrderBy(pk => pk.Address.ToString("raw", null))
                .Select(
                    (pk, i) => Transaction.Create(
                        nonce: i,
                        privateKey: privateKey,
                        genesisHash: default,
                        actions: new []
                        {
                            new Initialize
                            {
                                Validators = [Validator.Create(pk.PublicKey, 1)],
                                States = ImmutableDictionary.Create<Address, IValue>()
                            },
                        }.ToBytecodes()))
                .ToImmutableSortedSet());
        Chain = BlockChain.Create(
            policy,
            store,
            stateStore,
            genesisBlock);
        MinedBlocks = MinedBlocks.SetItem(
            Chain.Genesis.Proposer,
            ImmutableArray<Block>.Empty.Add(Chain.Genesis));

        while (Chain.Count < blockCount)
        {
            AddBlock(GetRandomTransactions());
        }

        if (txActionsForSuffixBlocks is { } txActionsForSuffixBlocksVal)
        {
            foreach (var actionsForTransactions in txActionsForSuffixBlocksVal)
            {
                var pk = PrivateKeys[Random.Next(PrivateKeys.Length)];
                AddBlock(actionsForTransactions
                    .Select(actions =>
                        Transaction.Create(
                            nonce: Chain.GetNextTxNonce(pk.Address),
                            privateKey: pk,
                            genesisHash: Chain.Genesis.BlockHash,
                            actions: actions.ToBytecodes()))
                    .ToImmutableArray());
            }
        }
    }

    private ImmutableArray<Transaction> GetRandomTransactions()
    {
        var nonces = ImmutableDictionary<PrivateKey, long>.Empty;
        return Enumerable
            .Range(0, Random.Next(MaxTxCount))
            .Select(_ =>
            {
                var pk = PrivateKeys[Random.Next(PrivateKeys.Length)];
                if (!nonces.TryGetValue(pk, out var nonce))
                {
                    nonce = Chain.GetNextTxNonce(pk.Address);
                }

                nonces = nonces.SetItem(pk, nonce + 1);

                return GetRandomTransaction(pk, nonce);
            })
            .OrderBy(tx => tx.Id)
            .ToImmutableArray();
    }

    private Transaction GetRandomTransaction(PrivateKey pk, long nonce)
    {
        return Transaction.Create(
            nonce: nonce,
            privateKey: pk,
            genesisHash: Chain.Genesis.BlockHash,
            actions: Random.Next() % 2 == 0
                ? GetRandomActions().ToBytecodes()
                : ImmutableHashSet<SimpleAction>.Empty.ToBytecodes(),
            maxGasPrice: null,
            gasLimit: 0L);
    }

    private ImmutableArray<SimpleAction> GetRandomActions()
    {
        return Enumerable
            .Range(0, Random.Next(10))
            .Select(_ => SimpleAction.GetAction(Random.Next()))
            .ToImmutableArray();
    }

    private void AddBlock(ImmutableArray<Transaction> transactions)
    {
        var proposer = PrivateKeys[Random.Next(PrivateKeys.Length)];
        var block = Chain.EvaluateAndSign(
            RawBlock.Create(
                new BlockHeader
                {
                    Height = Chain.Tip.Height + 1,
                    Timestamp = DateTimeOffset.UtcNow,
                    Proposer = proposer.Address,
                    PreviousHash = Chain.Tip.BlockHash,
                    LastCommit = Chain.Store.GetChainBlockCommit(Chain.Store.GetCanonicalChainId()!.Value),
                },
                new BlockContent
                {
                    Transactions = [.. transactions],
                    Evidences = [],
                }),
            proposer);
        Chain.Append(
            block,
            new BlockCommit
            {
                Height = Chain.Tip.Height + 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Votes = PrivateKeys
                    .OrderBy(pk => pk.Address.ToString("raw", null))
                    .Select(pk => new VoteMetadata
                    {
                        Height = Chain.Tip.Height + 1,
                        Round = 0,
                        BlockHash = block.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorPublicKey = pk.PublicKey,
                        ValidatorPower = BigInteger.One,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(pk))
                    .ToImmutableArray(),
            });
        MinedBlocks = MinedBlocks
            .SetItem(
                proposer.Address,
                MinedBlocks[proposer.Address].Add(block));
        SignedTxs = transactions.Aggregate(
            SignedTxs,
            (dict, tx) =>
                dict.SetItem(
                    tx.Signer,
                    dict[tx.Signer]
                        .Add(tx)
                        .OrderBy(tx => tx.Nonce)
                        .ToImmutableArray()));
    }
}
