using Libplanet.State;
using Libplanet.State.Builtin;
using Libplanet.Data;
using Libplanet.Types;
using Libplanet.TestUtilities;

namespace Libplanet.Explorer.Tests;

public class GeneratedBlockChainFixture
{
    public static Currency TestCurrency => Currency.Create("TEST", 0);

    public Blockchain Chain { get; }

    public Repository Repository { get; }

    public ImmutableArray<PrivateKey> PrivateKeys { get; }

    public ImmutableArray<ISigner> Signers { get; }

    public int MaxTxCount { get; }

    public ImmutableDictionary<Address, ImmutableArray<Block>> MinedBlocks { get; private set; }

    public ImmutableDictionary<Address, ImmutableArray<Transaction>> SignedTxs { get; private set; }

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

        Random = new System.Random(seed);
        MaxTxCount = maxTxCount;
        PrivateKeys = Enumerable
            .Range(0, privateKeyCount)
            .Select(_ => new PrivateKey())
            .ToImmutableArray();
        Signers = [.. PrivateKeys.Select(key => key.AsSigner())];
        MinedBlocks = PrivateKeys
            .ToImmutableDictionary(
                key => key.Address,
                key => ImmutableArray<Block>.Empty);
        SignedTxs = PrivateKeys
            .ToImmutableDictionary(
                key => key.Address,
                key => ImmutableArray<Transaction>.Empty);

        var signer = RandomUtility.Signer(Random);
        var options = new BlockchainOptions
        {
            // BlockInterval = TimeSpan.FromMilliseconds(1),
            BlockOptions = new BlockOptions
            {
                MaxTransactions = int.MaxValue,
                MaxActionBytes = long.MaxValue,
            },
        };
        var genesisBlock = new BlockBuilder
        {
            Transactions = PrivateKeys
                .OrderBy(pk => pk.Address.ToString("raw", null))
                .Select(
                    (pk, i) => new TransactionMetadata
                    {
                        Nonce = i,
                        Signer = signer.Address,
                        GenesisBlockHash = default,
                        Actions = new[]
                        {
                            new Initialize
                            {
                                Validators = [new Validator { Address = pk.Address }],
                            },
                        }.ToBytecodes(),
                    }.Sign(signer))
                .ToImmutableSortedSet(),
        }.Create(new PrivateKey().AsSigner());
        Repository = new Repository();
        Chain = new Blockchain(Repository, options);
        MinedBlocks = MinedBlocks.SetItem(
            Chain.Genesis.Proposer,
            ImmutableArray<Block>.Empty.Add(Chain.Genesis));

        while (Chain.Blocks.Count < blockCount)
        {
            AddBlock(GetRandomTransactions());
        }

        if (txActionsForSuffixBlocks is { } txActionsForSuffixBlocksVal)
        {
            foreach (var actionsForTransactions in txActionsForSuffixBlocksVal)
            {
                var pk = Signers[Random.Next(Signers.Length)];
                AddBlock(actionsForTransactions
                    .Select(actions =>
                        new TransactionMetadata
                        {
                            Nonce = Chain.GetNextTxNonce(pk.Address),
                            Signer = pk.Address,
                            GenesisBlockHash = Chain.Genesis.BlockHash,
                            Actions = actions.ToBytecodes(),
                        }.Sign(pk))
                    .ToImmutableArray());
            }
        }
    }

    private ImmutableArray<Transaction> GetRandomTransactions()
    {
        var nonces = ImmutableDictionary<ISigner, long>.Empty;
        return Enumerable
            .Range(0, Random.Next(MaxTxCount))
            .Select(_ =>
            {
                var signer = Signers[Random.Next(Signers.Length)];
                if (!nonces.TryGetValue(signer, out var nonce))
                {
                    nonce = Chain.GetNextTxNonce(signer.Address);
                }

                nonces = nonces.SetItem(signer, nonce + 1);

                return GetRandomTransaction(signer, nonce);
            })
            .OrderBy(tx => tx.Id)
            .ToImmutableArray();
    }

    private Transaction GetRandomTransaction(ISigner signer, long nonce)
    {
        return new TransactionMetadata
        {
            Nonce = nonce,
            Signer = signer.Address,
            GenesisBlockHash = Chain.Genesis.BlockHash,
            Actions = Random.Next() % 2 == 0
                ? GetRandomActions().ToBytecodes()
                : ImmutableHashSet<SimpleAction>.Empty.ToBytecodes(),
            MaxGasPrice = null,
            GasLimit = 0L,
        }.Sign(signer);
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
        var proposer = Signers[Random.Next(Signers.Length)];
        var block = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = Chain.Tip.Height + 1,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = proposer.Address,
                PreviousBlockHash = Chain.Tip.BlockHash,
                PreviousBlockCommit = Chain.BlockCommits[^1],
            },
            Content = new BlockContent
            {
                Transactions = [.. transactions],
                Evidences = [],
            },
        }.Sign(proposer);
        Chain.Append(
            block,
            new BlockCommit
            {
                Height = Chain.Tip.Height + 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Votes = Signers
                    .OrderBy(pk => pk.Address.ToString("raw", null))
                    .Select(pk => new VoteMetadata
                    {
                        Height = Chain.Tip.Height + 1,
                        Round = 0,
                        BlockHash = block.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = pk.Address,
                        ValidatorPower = BigInteger.One,
                        Type = VoteType.PreCommit,
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
