using System.Security.Cryptography;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Types;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Store;

public abstract class RepositoryTest
{
    protected abstract RepositoryFixture Fx { get; }

    protected abstract Func<RepositoryFixture> FxConstructor { get; }

    [Fact]
    public void BlockDigests_Test()
    {
        var repository = Fx.Repository;
        Assert.Empty(repository.BlockDigests.Keys);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block1.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block2.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block3.BlockHash]);
        Assert.False(repository.BlockDigests.Remove(Fx.Block1.BlockHash));
        Assert.False(repository.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.False(repository.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.False(repository.BlockDigests.ContainsKey(Fx.Block3.BlockHash));

        repository.BlockDigests.Add(Fx.Block1);
        Assert.Single(repository.BlockDigests);
        Assert.Equal([Fx.Block1.BlockHash], repository.BlockDigests.Keys);
        Assert.Equal(Fx.Block1, repository.GetBlock(Fx.Block1.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block2.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block3.BlockHash]);
        Assert.Equal(Fx.Block1.Height, repository.BlockDigests[Fx.Block1.BlockHash].Height);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block2.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block3.BlockHash]);
        Assert.True(repository.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.False(repository.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.False(repository.BlockDigests.ContainsKey(Fx.Block3.BlockHash));

        repository.BlockDigests.Add(Fx.Block2);
        Assert.Equal(2, repository.BlockDigests.Count);
        Assert.Equal(
            new HashSet<BlockHash> { Fx.Block1.BlockHash, Fx.Block2.BlockHash },
            [.. repository.BlockDigests.Keys]);
        Assert.Equal(Fx.Block1, repository.GetBlock(Fx.Block1.BlockHash));
        Assert.Equal(Fx.Block2, repository.GetBlock(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block3.BlockHash]);
        Assert.Equal(Fx.Block1.Height, repository.BlockDigests[Fx.Block1.BlockHash].Height);
        Assert.Equal(Fx.Block2.Height, repository.BlockDigests[Fx.Block2.BlockHash].Height);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block3.BlockHash]);
        Assert.True(repository.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.True(repository.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.False(repository.BlockDigests.ContainsKey(Fx.Block3.BlockHash));

        Assert.True(repository.BlockDigests.Remove(Fx.Block1.BlockHash));
        Assert.Single(repository.BlockDigests);
        Assert.Equal([Fx.Block2.BlockHash], repository.BlockDigests.Keys);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block1.BlockHash]);
        Assert.Equal(Fx.Block2, repository.GetBlock(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block3.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block1.BlockHash]);
        Assert.Equal(Fx.Block2.Height, repository.BlockDigests[Fx.Block2.BlockHash].Height);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockDigests[Fx.Block3.BlockHash]);
        Assert.False(repository.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.True(repository.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.False(repository.BlockDigests.ContainsKey(Fx.Block3.BlockHash));
    }

    // [Fact]
    // public void TxExecution()
    // {
    //     static void AssertTxExecutionEqual(TxExecution expected, TxExecution actual)
    //     {
    //         Assert.Equal(expected.Fail, actual.Fail);
    //         Assert.Equal(expected.TxId, actual.TxId);
    //         Assert.Equal(expected.BlockHash, actual.BlockHash);
    //         Assert.Equal(expected.InputState, actual.InputState);
    //         Assert.Equal(expected.OutputState, actual.OutputState);
    //         Assert.Equal(expected.ExceptionNames, actual.ExceptionNames);
    //     }

    //     var store = Fx.Store;

    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1, Fx.Hash1]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash1]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1, Fx.Hash2]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash2]);

    //     var inputA = new TxExecution
    //     {
    //         BlockHash = Fx.Hash1,
    //         TxId = Fx.TxId1,
    //         InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
    //         OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
    //         ExceptionNames = [],
    //     };
    //     store.TxExecutions.Add(inputA);

    //     AssertTxExecutionEqual(inputA, store.TxExecutions[Fx.TxId1, Fx.Hash1]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash1]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1, Fx.Hash2]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash2]);

    //     var inputB = new TxExecution
    //     {
    //         BlockHash = Fx.Hash1,
    //         TxId = Fx.TxId2,
    //         InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
    //         OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
    //         ExceptionNames = ["AnExceptionName"],
    //     };
    //     store.TxExecutions.Add(inputB);

    //     AssertTxExecutionEqual(inputA, store.TxExecutions[Fx.TxId1, Fx.Hash1]);
    //     AssertTxExecutionEqual(inputB, store.TxExecutions[Fx.TxId2, Fx.Hash1]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash2]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1, Fx.Hash2]);

    //     var inputC = new TxExecution
    //     {
    //         BlockHash = Fx.Hash2,
    //         TxId = Fx.TxId1,
    //         InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
    //         OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
    //         ExceptionNames = ["AnotherExceptionName", "YetAnotherExceptionName"],
    //     };
    //     store.TxExecutions.Add(inputC);

    //     AssertTxExecutionEqual(inputA, store.TxExecutions[Fx.TxId1, Fx.Hash1]);
    //     AssertTxExecutionEqual(inputB, store.TxExecutions[Fx.TxId2, Fx.Hash1]);
    //     AssertTxExecutionEqual(inputC, store.TxExecutions[Fx.TxId1, Fx.Hash2]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash2]);
    // }

    // [Fact]
    // public void TxIdBlockHashIndex()
    // {
    //     var store = Fx.Store;
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId3]);

    //     store.TxExecutions.Add(new TxExecution
    //     {
    //         TxId = Fx.TxId1,
    //         BlockHash = Fx.Hash1,
    //     });
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId3]);

    //     store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId2, BlockHash = Fx.Hash2 });
    //     store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId3, BlockHash = Fx.Hash3 });

    //     Assert.Single(store.TxExecutions[Fx.TxId1].Select(item => item.BlockHash), Fx.Hash1);
    //     Assert.Single(store.TxExecutions[Fx.TxId2].Select(item => item.BlockHash), Fx.Hash2);
    //     Assert.Single(store.TxExecutions[Fx.TxId3].Select(item => item.BlockHash), Fx.Hash3);

    //     store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId1, BlockHash = Fx.Hash3 });
    //     store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId2, BlockHash = Fx.Hash3 });
    //     store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId3, BlockHash = Fx.Hash1 });
    //     Assert.Equal(2, store.TxExecutions[Fx.TxId1].Length);
    //     Assert.Equal(2, store.TxExecutions[Fx.TxId2].Length);
    //     Assert.Equal(2, store.TxExecutions[Fx.TxId3].Length);

    //     Assert.True(store.TxExecutions.Remove(Fx.TxId1, Fx.Hash1));
    //     Assert.True(store.TxExecutions.Remove(Fx.TxId2, Fx.Hash2));
    //     Assert.True(store.TxExecutions.Remove(Fx.TxId3, Fx.Hash3));

    //     Assert.Single(store.TxExecutions[Fx.TxId1].Select(item => item.BlockHash), Fx.Hash3);
    //     Assert.Single(store.TxExecutions[Fx.TxId2].Select(item => item.BlockHash), Fx.Hash3);
    //     Assert.Single(store.TxExecutions[Fx.TxId3].Select(item => item.BlockHash), Fx.Hash1);

    //     Assert.False(store.TxExecutions.Remove(Fx.TxId1, Fx.Hash1));
    //     Assert.False(store.TxExecutions.Remove(Fx.TxId2, Fx.Hash2));
    //     Assert.False(store.TxExecutions.Remove(Fx.TxId3, Fx.Hash3));

    //     Assert.True(store.TxExecutions.Remove(Fx.TxId1, Fx.Hash3));
    //     Assert.True(store.TxExecutions.Remove(Fx.TxId2, Fx.Hash3));
    //     Assert.True(store.TxExecutions.Remove(Fx.TxId3, Fx.Hash1));

    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2]);
    //     Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId3]);
    // }

    [Fact]
    public void PendingTransactions_Test()
    {
        var store = Fx.Repository;
        Assert.Throws<KeyNotFoundException>(() => store.PendingTransactions[Fx.Transaction1.Id]);
        Assert.Throws<KeyNotFoundException>(() => store.PendingTransactions[Fx.Transaction2.Id]);
        Assert.False(store.PendingTransactions.ContainsKey(Fx.Transaction1.Id));
        Assert.False(store.PendingTransactions.ContainsKey(Fx.Transaction2.Id));

        store.PendingTransactions.Add(Fx.Transaction1);
        Assert.Equal(
            Fx.Transaction1,
            store.PendingTransactions[Fx.Transaction1.Id]);
        Assert.Throws<KeyNotFoundException>(() => store.PendingTransactions[Fx.Transaction2.Id]);
        Assert.True(store.PendingTransactions.ContainsKey(Fx.Transaction1.Id));
        Assert.False(store.PendingTransactions.ContainsKey(Fx.Transaction2.Id));

        store.PendingTransactions.Add(Fx.Transaction2);
        Assert.Equal(
            Fx.Transaction1,
            store.PendingTransactions[Fx.Transaction1.Id]);
        Assert.Equal(
            Fx.Transaction2,
            store.PendingTransactions[Fx.Transaction2.Id]);
        Assert.True(store.PendingTransactions.ContainsKey(Fx.Transaction1.Id));
        Assert.True(store.PendingTransactions.ContainsKey(Fx.Transaction2.Id));

        Assert.Equal(
            Fx.Transaction2,
            store.PendingTransactions[Fx.Transaction2.Id]);
        Assert.True(store.PendingTransactions.ContainsKey(Fx.Transaction2.Id));
    }

    [Fact]
    public void CommittedTransactions_Test()
    {
        var store = Fx.Repository;
        Assert.Throws<KeyNotFoundException>(() => store.CommittedTransactions[Fx.Transaction1.Id]);
        Assert.Throws<KeyNotFoundException>(() => store.CommittedTransactions[Fx.Transaction2.Id]);
        Assert.False(store.CommittedTransactions.ContainsKey(Fx.Transaction1.Id));
        Assert.False(store.CommittedTransactions.ContainsKey(Fx.Transaction2.Id));

        store.CommittedTransactions.Add(Fx.Transaction1);
        Assert.Equal(
            Fx.Transaction1,
            store.CommittedTransactions[Fx.Transaction1.Id]);
        Assert.Throws<KeyNotFoundException>(() => store.CommittedTransactions[Fx.Transaction2.Id]);
        Assert.True(store.CommittedTransactions.ContainsKey(Fx.Transaction1.Id));
        Assert.False(store.CommittedTransactions.ContainsKey(Fx.Transaction2.Id));

        store.CommittedTransactions.Add(Fx.Transaction2);
        Assert.Equal(
            Fx.Transaction1,
            store.CommittedTransactions[Fx.Transaction1.Id]);
        Assert.Equal(
            Fx.Transaction2,
            store.CommittedTransactions[Fx.Transaction2.Id]);
        Assert.True(store.CommittedTransactions.ContainsKey(Fx.Transaction1.Id));
        Assert.True(store.CommittedTransactions.ContainsKey(Fx.Transaction2.Id));

        Assert.Equal(
            Fx.Transaction2,
            store.CommittedTransactions[Fx.Transaction2.Id]);
        Assert.True(store.CommittedTransactions.ContainsKey(Fx.Transaction2.Id));
    }

    [Fact]
    public void BlockHashes_Test()
    {
        var repository = Fx.Repository;
        Assert.Throws<KeyNotFoundException>(() => repository.BlockHashes[0]);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockHashes[^1]);

        repository.Append(Fx.GenesisBlock, BlockCommit.Empty);
        repository.GenesisHeight = Fx.GenesisBlock.Height;
        repository.Height = Fx.GenesisBlock.Height;
        Assert.Equal(0, repository.Height);

        repository.BlockHashes.Add(Fx.Block1);
        repository.Height = Fx.Block1.Height;
        Assert.Equal(1, repository.Height);
        Assert.Equal(
            [Fx.Block1.BlockHash],
            repository.BlockHashes[1..]);
        Assert.Equal(Fx.Block1.BlockHash, repository.BlockHashes[1]);
        Assert.Equal(Fx.Block1.BlockHash, repository.BlockHashes[^1]);

        repository.BlockHashes.Add(Fx.Block2);
        repository.Height = Fx.Block2.Height;
        Assert.Equal(2, repository.Height);
        Assert.Equal(
            [Fx.Block1.BlockHash, Fx.Block2.BlockHash],
            repository.BlockHashes[1..]);
        Assert.Equal(Fx.Block1.BlockHash, repository.BlockHashes[1]);
        Assert.Equal(Fx.Block2.BlockHash, repository.BlockHashes[2]);
        Assert.Equal(Fx.Block2.BlockHash, repository.BlockHashes[^1]);
        Assert.Equal(Fx.Block1.BlockHash, repository.BlockHashes[^2]);
    }

    [Fact]
    public void BlockHashes_Iteration_Test()
    {
        var repository = Fx.Repository;

        repository.BlockHashes.Add(0, Fx.Hash1);
        repository.BlockHashes.Add(1, Fx.Hash2);
        repository.BlockHashes.Add(2, Fx.Hash3);

        Assert.Equal(
            [Fx.Hash1, Fx.Hash2, Fx.Hash3],
            repository.BlockHashes[..]);
        Assert.Equal(
            [Fx.Hash2, Fx.Hash3],
            repository.BlockHashes[1..]);
        Assert.Equal(
            [Fx.Hash3],
            repository.BlockHashes[2..]);
        Assert.Equal([], repository.BlockHashes[3..4]);
        Assert.Equal([], repository.BlockHashes[4..5]);
        Assert.Equal([], repository.BlockHashes[0..0]);
        Assert.Equal(
            [Fx.Hash1],
            repository.BlockHashes[0..1]);
        Assert.Equal(
            [Fx.Hash1, Fx.Hash2],
            repository.BlockHashes[0..2]);
        Assert.Equal(
            [Fx.Hash1, Fx.Hash2, Fx.Hash3],
            repository.BlockHashes[0..3]);
        Assert.Equal(
            [Fx.Hash1, Fx.Hash2, Fx.Hash3],
            repository.BlockHashes[0..^0]);
        Assert.Equal(
            [Fx.Hash2],
            repository.BlockHashes[1..2]);
    }

    [Fact]
    public void Nonces_Test()
    {
        var repository = Fx.Repository;
        Assert.Equal(0, repository.GetNonce(Fx.Transaction1.Signer));
        Assert.Equal(0, repository.GetNonce(Fx.Transaction2.Signer));

        repository.Nonces.Increase(Fx.Transaction1.Signer);
        Assert.Equal(1, repository.GetNonce(Fx.Transaction1.Signer));
        Assert.Equal(0, repository.GetNonce(Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 1,
            }.ToImmutableSortedDictionary(),
            repository.Nonces.ToImmutableSortedDictionary());

        repository.Nonces.Increase(Fx.Transaction2.Signer, 5);
        Assert.Equal(1, repository.GetNonce(Fx.Transaction1.Signer));
        Assert.Equal(5, repository.GetNonce(Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 1,
                [Fx.Transaction2.Signer] = 5,
            }.ToImmutableSortedDictionary(),
            repository.Nonces.ToImmutableSortedDictionary());

        repository.Nonces.Increase(Fx.Transaction1.Signer, 2);
        Assert.Equal(3, repository.GetNonce(Fx.Transaction1.Signer));
        Assert.Equal(5, repository.GetNonce(Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 3,
                [Fx.Transaction2.Signer] = 5,
            }.ToImmutableSortedDictionary(),
            repository.Nonces.ToImmutableSortedDictionary());
    }

    [Fact]
    public void IndexBlockHashReturnNull()
    {
        var repository = Fx.Repository;
        repository.BlockDigests.Add(Fx.Block1);
        repository.BlockHashes.Add(1, Fx.Block1.BlockHash);
        repository.Height = Fx.Block1.Height;
        Assert.Equal(1, repository.Height);
        Assert.Throws<KeyNotFoundException>(() => repository.BlockHashes[2]);
    }

    [Fact]
    public async Task TxAtomicity()
    {
        Transaction MakeTx(
            System.Random random,
            MD5 md5,
            PrivateKey key,
            int txNonce)
        {
            byte[] arbitraryBytes = new byte[20];
            random.NextBytes(arbitraryBytes);
            byte[] digest = md5.ComputeHash(arbitraryBytes);
            var action = new AtomicityTestAction
            {
                ArbitraryBytes = [.. arbitraryBytes],
                Md5Digest = [.. digest],
            };
            return new TransactionMetadata
            {
                Nonce = txNonce,
                Signer = key.Address,
                GenesisHash = default,
                Actions = new[] { action }.ToBytecodes(),
                Timestamp = DateTimeOffset.UtcNow,
            }.Sign(key);
        }

        const int taskCount = 5;
        const int txCount = 30;
        var store = Fx.Repository;
        var md5Hasher = MD5.Create();
        Transaction commonTx = MakeTx(
            new System.Random(),
            md5Hasher,
            new PrivateKey(),
            0);
        Task[] tasks = new Task[taskCount];
        for (int i = 0; i < taskCount; i++)
        {
            var task = new Task(() =>
            {
                var key = new PrivateKey();
                var random = new System.Random();
                var md5 = MD5.Create();
                Transaction tx;
                for (int j = 0; j < 50; j++)
                {
                    store.PendingTransactions.TryAdd(commonTx.Id, commonTx);
                }

                for (int j = 0; j < txCount; j++)
                {
                    tx = MakeTx(random, md5, key, j + 1);
                    store.PendingTransactions.TryAdd(tx.Id, tx);
                }
            });
            task.Start();
            tasks[i] = task;
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (AggregateException e)
        {
            throw;
        }
    }

    // [Fact]
    // public void Copy()
    // {
    //     using var fx1 = FxConstructor();
    //     using var fx2 = FxConstructor();
    //     var s1 = fx1.Store;
    //     var s2 = fx2.Store;
    //     var preEval = ProposeGenesis(proposer: GenesisProposer.PublicKey);
    //     var genesis = preEval.Sign(
    //         GenesisProposer,
    //         default);
    //     var blockChain = new BlockChain(genesis, fx1.Options);

    //     var key = new PrivateKey();
    //     var block = blockChain.ProposeBlock(key);
    //     blockChain.Append(block, CreateBlockCommit(block));
    //     block = blockChain.ProposeBlock(key, CreateBlockCommit(blockChain.Tip));
    //     blockChain.Append(block, CreateBlockCommit(block));
    //     block = blockChain.ProposeBlock(key, CreateBlockCommit(blockChain.Tip));
    //     blockChain.Append(block, CreateBlockCommit(block));

    //     s1.Copy(to: Fx.Store);
    //     store.Copy(to: s2);

    //     Assert.Equal(s1.Chains.Keys.ToHashSet(), [.. s2.Chains.Keys]);
    //     Assert.Equal(s1.ChainId, s2.ChainId);
    //     foreach (Guid chainId in s1.Chains.Keys)
    //     {
    //         Assert.Equal(s1.GetBlockHashes(chainId).IterateHeights(), s2.GetBlockHashes(chainId).IterateHeights());
    //         foreach (BlockHash blockHash in s1.GetBlockHashes(chainId).IterateHeights())
    //         {
    //             Assert.Equal(s1.Blocks[blockHash], s2.Blocks[blockHash]);
    //         }
    //     }

    //     // ArgumentException is thrown if the destination store is not empty.
    //     Assert.Throws<ArgumentException>(() => store.Copy(fx2.Store));
    // }

    [Fact]
    public void GetBlock_Test()
    {
        using var fx = FxConstructor();
        var store = fx.Repository;
        var genesisBlock = fx.GenesisBlock;
        var expectedBlock = ProposeNextBlock(genesisBlock, fx.Proposer);

        store.BlockDigests.Add(expectedBlock);
        var actualBlock = store.GetBlock(expectedBlock.BlockHash);

        Assert.Equal(expectedBlock, actualBlock);
    }

    [Fact]
    public void GetBlockCommit_Test()
    {
        using var fx = FxConstructor();
        var store = fx.Repository;
        var height = 1;
        var round = 0;
        var hash = fx.Block2.BlockHash;
        var validators = Enumerable.Range(0, 4)
            .Select(x => new PrivateKey())
            .ToArray();
        var votes = validators.Select(validator => new VoteMetadata
        {
            Height = height,
            Round = round,
            BlockHash = hash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validator.Address,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(validator)).ToImmutableArray();

        var expectedCommit = new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = hash,
            Votes = votes,
        };
        store.BlockCommits.Add(expectedCommit);

        var actualCommit = store.BlockCommits[expectedCommit.BlockHash];

        Assert.Equal(expectedCommit, actualCommit);
    }

    [Fact]
    public void GetBlockCommitIndices()
    {
        using var fx = FxConstructor();
        var votesOne = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = fx.Block1.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = fx.Proposer.Address,
                ValidatorPower = fx.ProposerPower,
                Flag = VoteFlag.PreCommit,
            }.Sign(fx.Proposer));
        var votesTwo = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = fx.Block2.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = fx.Proposer.Address,
                ValidatorPower = fx.ProposerPower,
                Flag = VoteFlag.PreCommit,
            }.Sign(fx.Proposer));

        BlockCommit[] blockCommits =
        [
            new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = fx.Block1.BlockHash,
                Votes = votesOne,
            },
            new BlockCommit
            {
                Height = 2,
                Round = 0,
                BlockHash = fx.Block2.BlockHash,
                Votes = votesTwo,
            },
        ];

        fx.Repository.BlockCommits.AddRange(blockCommits);

        var actualHeight = fx.Repository.BlockCommits.Values.Select(item => item.Height).ToImmutableSortedSet();

        Assert.Equal([1, 2], actualHeight);
    }

    [Fact]
    public void DeleteLastCommit()
    {
        using var fx = FxConstructor();
        var store = fx.Repository;
        var validatorPrivateKey = new PrivateKey();
        var blockCommit = new BlockCommit
        {
            BlockHash = Fx.GenesisBlock.BlockHash,
            Votes =
            [
                new VoteMetadata
                {
                    BlockHash = Fx.GenesisBlock.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = validatorPrivateKey.Address,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(validatorPrivateKey)
            ],
        };

        store.BlockCommits.Add(blockCommit);
        Assert.Equal(blockCommit, store.BlockCommits[blockCommit.BlockHash]);

        store.BlockCommits.Remove(blockCommit.BlockHash);
        Assert.Throws<KeyNotFoundException>(() => store.BlockCommits[blockCommit.BlockHash]);
    }

    [Fact]
    public void IteratePendingEvidenceIds()
    {
        using var fx = FxConstructor();
        var store = fx.Repository;
        var signer = TestUtils.ValidatorPrivateKeys[0];
        var duplicateVoteOne = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = fx.Block1.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(signer))
            .Add(new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = fx.Block2.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(signer));
        var duplicateVoteTwo = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = fx.Block2.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(signer))
            .Add(new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = fx.Block3.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(signer));

        EvidenceBase[] evidences =
        [
            DuplicateVoteEvidence.Create(duplicateVoteOne[0], duplicateVoteOne[1], TestUtils.Validators),
            DuplicateVoteEvidence.Create(duplicateVoteTwo[0], duplicateVoteTwo[1], TestUtils.Validators),
        ];

        store.PendingEvidences.AddRange(evidences);

        Assert.Equal(
            [.. evidences.Select(e => e.Id)],
            store.PendingEvidences.Keys.ToImmutableSortedSet());
    }

    [Fact]
    public void ManipulatePendingEvidence()
    {
        var store = Fx.Repository;
        var signer = TestUtils.ValidatorPrivateKeys[0];
        var duplicateVote = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = Fx.Block1.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(signer))
            .Add(new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = Fx.Block2.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(signer));
        var evidence = DuplicateVoteEvidence.Create(duplicateVote[0], duplicateVote[1], TestUtils.Validators);

        Assert.DoesNotContain(evidence.Id, store.PendingEvidences.Keys);

        store.PendingEvidences.Add(evidence);
        EvidenceBase storedEvidence = store.PendingEvidences[evidence.Id];

        Assert.Equal(evidence, storedEvidence);
        Assert.Contains(evidence.Id, store.PendingEvidences.Keys);

        store.PendingEvidences.Remove(evidence.Id);
        Assert.DoesNotContain(evidence.Id, store.PendingEvidences.Keys);
    }

    [Fact]
    public void ManipulateCommittedEvidence()
    {
        using var fx = FxConstructor();
        var store = fx.Repository;
        var signer = TestUtils.ValidatorPrivateKeys[0];
        var duplicateVote = ImmutableArray<Vote>.Empty
            .Add(new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = fx.Block1.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(signer))
            .Add(new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = fx.Block2.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(signer));
        var evidence = DuplicateVoteEvidence.Create(duplicateVote[0], duplicateVote[1], TestUtils.Validators);

        Assert.DoesNotContain(evidence.Id, store.CommittedEvidences.Keys);

        store.CommittedEvidences.Add(evidence);
        var storedEvidence = store.CommittedEvidences[evidence.Id];

        Assert.Equal(evidence, storedEvidence);
        Assert.Contains(evidence.Id, store.CommittedEvidences.Keys);

        store.CommittedEvidences.Remove(evidence);
        Assert.DoesNotContain(evidence.Id, store.CommittedEvidences.Keys);
    }

    [Fact]
    public void IdempotentDispose()
    {
        var store = Fx.Repository;
        store.Dispose();
        store.Dispose();

        Assert.True(true, "Disposing twice should not throw.");
    }

    [Model(Version = 1)]
    private sealed record class AtomicityTestAction : ActionBase, IEquatable<AtomicityTestAction>
    {
        [Property(0)]
        public ImmutableArray<byte> ArbitraryBytes { get; set; }

        [Property(1)]
        public ImmutableArray<byte> Md5Digest { get; set; }

        public override int GetHashCode() => ModelResolver.GetHashCode(this);

        public bool Equals(AtomicityTestAction? other) => ModelResolver.Equals(this, other);

        protected override void OnExecute(IWorldContext world, IActionContext context)
        {
        }
    }
}
