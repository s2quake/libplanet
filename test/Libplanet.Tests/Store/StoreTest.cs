using System.Security.Cryptography;
using System.Threading.Tasks;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;
using Serilog;
using Xunit.Abstractions;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Store;

public abstract class StoreTest
{
    private ILogger? _logger = null;

    protected abstract ITestOutputHelper TestOutputHelper { get; }

    protected abstract StoreFixture Fx { get; }

    protected abstract Func<StoreFixture> FxConstructor { get; }

    protected ILogger Logger => _logger ??= new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.TestOutput(TestOutputHelper)
        .CreateLogger()
        .ForContext(this.GetType());

    [Fact]
    public void ListChainId()
    {
        Assert.Empty(Fx.Store.ListChainIds());

        Fx.Store.PutBlock(Fx.Block1);
        Fx.Store.AppendIndex(Fx.StoreChainId, Fx.Block1.BlockHash);
        Assert.Equal(
            new[] { Fx.StoreChainId }.ToImmutableHashSet(),
            [.. Fx.Store.ListChainIds()]);

        Guid arbitraryGuid = Guid.NewGuid();
        Fx.Store.AppendIndex(arbitraryGuid, Fx.Block1.BlockHash);
        Assert.Equal(
            new[] { Fx.StoreChainId, arbitraryGuid }.ToImmutableHashSet(),
            [.. Fx.Store.ListChainIds()]);
    }

    [Fact]
    public void ListChainIdAfterForkAndDelete()
    {
        var chainA = Guid.NewGuid();
        var chainB = Guid.NewGuid();

        Fx.Store.PutBlock(Fx.GenesisBlock);
        Fx.Store.PutBlock(Fx.Block1);
        Fx.Store.PutBlock(Fx.Block2);

        Fx.Store.AppendIndex(chainA, Fx.GenesisBlock.BlockHash);
        Fx.Store.AppendIndex(chainA, Fx.Block1.BlockHash);
        Fx.Store.ForkBlockIndexes(chainA, chainB, Fx.Block1.BlockHash);
        Fx.Store.AppendIndex(chainB, Fx.Block2.BlockHash);

        Fx.Store.DeleteChainId(chainA);

        Assert.Equal(
            new[] { chainB }.ToImmutableHashSet(),
            [.. Fx.Store.ListChainIds()]);
    }

    [Fact]
    public void DeleteChainId()
    {
        Block block1 = ProposeNextBlock(
            ProposeGenesisBlock(GenesisProposer),
            GenesisProposer,
            [Fx.Transaction1]);
        Fx.Store.AppendIndex(Fx.StoreChainId, block1.BlockHash);
        Guid arbitraryChainId = Guid.NewGuid();
        Fx.Store.AppendIndex(arbitraryChainId, block1.BlockHash);
        Fx.Store.IncreaseTxNonce(Fx.StoreChainId, Fx.Transaction1.Signer);

        Fx.Store.DeleteChainId(Fx.StoreChainId);

        Assert.Equal(
            new[] { arbitraryChainId }.ToImmutableHashSet(),
            [.. Fx.Store.ListChainIds()]);
        Assert.Equal(0, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction1.Signer));
    }

    [Fact]
    public void DeleteChainIdIsIdempotent()
    {
        Assert.Empty(Fx.Store.ListChainIds());
        Fx.Store.DeleteChainId(Guid.NewGuid());
        Assert.Empty(Fx.Store.ListChainIds());
    }

    [Fact]
    public void DeleteChainIdWithForks()
    {
        Skip.IfNot(
            Environment.GetEnvironmentVariable("XUNIT_UNITY_RUNNER") is null,
            "Flaky test : Libplanet.Blocks.InvalidBlockSignatureException");

        Libplanet.Store.Store store = Fx.Store;
        Guid chainA = Guid.NewGuid();
        Guid chainB = Guid.NewGuid();
        Guid chainC = Guid.NewGuid();

        // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
        // actual block...
        store.PutBlock(Fx.GenesisBlock);
        store.PutBlock(Fx.Block1);
        store.PutBlock(Fx.Block2);
        store.PutBlock(Fx.Block3);

        store.AppendIndex(chainA, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(chainB, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(chainC, Fx.GenesisBlock.BlockHash);

        store.AppendIndex(chainA, Fx.Block1.BlockHash);
        store.ForkBlockIndexes(chainA, chainB, Fx.Block1.BlockHash);
        store.AppendIndex(chainB, Fx.Block2.BlockHash);
        store.ForkBlockIndexes(chainB, chainC, Fx.Block2.BlockHash);
        store.AppendIndex(chainC, Fx.Block3.BlockHash);

        // Deleting chainA doesn't effect chainB, chainC
        store.DeleteChainId(chainA);

        Assert.Empty(store.IterateIndexes(chainA));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainA, 0));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainA, 1));

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
            ],
            store.IterateIndexes(chainB));
        Assert.Equal(Fx.GenesisBlock.BlockHash, store.GetBlockHash(chainB, 0));
        Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHash(chainB, 1));
        Assert.Equal(Fx.Block2.BlockHash, store.GetBlockHash(chainB, 2));

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
            ],
            store.IterateIndexes(chainC));
        Assert.Equal(Fx.GenesisBlock.BlockHash, store.GetBlockHash(chainC, 0));
        Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHash(chainC, 1));
        Assert.Equal(Fx.Block2.BlockHash, store.GetBlockHash(chainC, 2));
        Assert.Equal(Fx.Block3.BlockHash, store.GetBlockHash(chainC, 3));

        // Deleting chainB doesn't effect chainC
        store.DeleteChainId(chainB);

        Assert.Empty(store.IterateIndexes(chainA));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainA, 0));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainA, 1));

        Assert.Empty(store.IterateIndexes(chainB));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainB, 0));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainB, 1));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainB, 2));

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
            ],
            store.IterateIndexes(chainC));
        Assert.Equal(Fx.GenesisBlock.BlockHash, store.GetBlockHash(chainC, 0));
        Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHash(chainC, 1));
        Assert.Equal(Fx.Block2.BlockHash, store.GetBlockHash(chainC, 2));
        Assert.Equal(Fx.Block3.BlockHash, store.GetBlockHash(chainC, 3));

        store.DeleteChainId(chainC);

        Assert.Empty(store.IterateIndexes(chainA));
        Assert.Empty(store.IterateIndexes(chainB));
        Assert.Empty(store.IterateIndexes(chainC));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainC, 0));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainC, 1));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainC, 2));
        Assert.Throws<KeyNotFoundException>(() => store.GetBlockHash(chainC, 3));
    }

    [Fact]
    public void DeleteChainIdWithForksReverse()
    {
        Libplanet.Store.Store store = Fx.Store;
        Guid chainA = Guid.NewGuid();
        Guid chainB = Guid.NewGuid();
        Guid chainC = Guid.NewGuid();

        // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
        // actual block...
        store.PutBlock(Fx.GenesisBlock);
        store.PutBlock(Fx.Block1);
        store.PutBlock(Fx.Block2);
        store.PutBlock(Fx.Block3);

        store.AppendIndex(chainA, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(chainB, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(chainC, Fx.GenesisBlock.BlockHash);

        store.AppendIndex(chainA, Fx.Block1.BlockHash);
        store.ForkBlockIndexes(chainA, chainB, Fx.Block1.BlockHash);
        store.AppendIndex(chainB, Fx.Block2.BlockHash);
        store.ForkBlockIndexes(chainB, chainC, Fx.Block2.BlockHash);
        store.AppendIndex(chainC, Fx.Block3.BlockHash);

        store.DeleteChainId(chainC);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
            ],
            store.IterateIndexes(chainA));
        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
            ],
            store.IterateIndexes(chainB));
        Assert.Empty(store.IterateIndexes(chainC));

        store.DeleteChainId(chainB);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
            ],
            store.IterateIndexes(chainA));
        Assert.Empty(store.IterateIndexes(chainB));
        Assert.Empty(store.IterateIndexes(chainC));

        store.DeleteChainId(chainA);
        Assert.Empty(store.IterateIndexes(chainA));
        Assert.Empty(store.IterateIndexes(chainB));
        Assert.Empty(store.IterateIndexes(chainC));
    }

    [Fact]
    public void ForkFromChainWithDeletion()
    {
        Libplanet.Store.Store store = Fx.Store;
        Guid chainA = Guid.NewGuid();
        Guid chainB = Guid.NewGuid();
        Guid chainC = Guid.NewGuid();

        // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
        // actual block...
        store.PutBlock(Fx.GenesisBlock);
        store.PutBlock(Fx.Block1);
        store.PutBlock(Fx.Block2);
        store.PutBlock(Fx.Block3);

        store.AppendIndex(chainA, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(chainA, Fx.Block1.BlockHash);
        store.ForkBlockIndexes(chainA, chainB, Fx.Block1.BlockHash);
        store.DeleteChainId(chainA);

        store.ForkBlockIndexes(chainB, chainC, Fx.Block1.BlockHash);
        Assert.Equal(
            Fx.Block1.BlockHash,
            store.GetBlockHash(chainC, Fx.Block1.Height));
    }

    [Fact]
    public void CanonicalChainId()
    {
        Assert.Equal(Guid.Empty, Fx.Store.ChainId);
        Guid a = Guid.NewGuid();
        Fx.Store.ChainId = a;
        Assert.Equal(a, Fx.Store.ChainId);
        Guid b = Guid.NewGuid();
        Fx.Store.ChainId = b;
        Assert.Equal(b, Fx.Store.ChainId);
    }

    [Fact]
    public void StoreBlock()
    {
        Assert.Empty(Fx.Store.IterateBlockHashes());
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlock(Fx.Block1.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlock(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlock(Fx.Block3.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHeight(Fx.Block1.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHeight(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHeight(Fx.Block3.BlockHash));
        Assert.False(Fx.Store.DeleteBlock(Fx.Block1.BlockHash));
        Assert.False(Fx.Store.ContainsBlock(Fx.Block1.BlockHash));
        Assert.False(Fx.Store.ContainsBlock(Fx.Block2.BlockHash));
        Assert.False(Fx.Store.ContainsBlock(Fx.Block3.BlockHash));

        Fx.Store.PutBlock(Fx.Block1);
        Assert.Equal(1, Fx.Store.CountBlocks());
        Assert.Equal(
            new HashSet<BlockHash> { Fx.Block1.BlockHash },
            [.. Fx.Store.IterateBlockHashes()]);
        Assert.Equal(
            Fx.Block1,
            Fx.Store.GetBlock(Fx.Block1.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlock(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlock(Fx.Block3.BlockHash));
        Assert.Equal(Fx.Block1.Height, Fx.Store.GetBlockHeight(Fx.Block1.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHeight(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHeight(Fx.Block3.BlockHash));
        Assert.True(Fx.Store.ContainsBlock(Fx.Block1.BlockHash));
        Assert.False(Fx.Store.ContainsBlock(Fx.Block2.BlockHash));
        Assert.False(Fx.Store.ContainsBlock(Fx.Block3.BlockHash));

        Fx.Store.PutBlock(Fx.Block2);
        Assert.Equal(2, Fx.Store.CountBlocks());
        Assert.Equal(
            new HashSet<BlockHash> { Fx.Block1.BlockHash, Fx.Block2.BlockHash },
            [.. Fx.Store.IterateBlockHashes()]);
        Assert.Equal(
            Fx.Block1,
            Fx.Store.GetBlock(Fx.Block1.BlockHash));
        Assert.Equal(
            Fx.Block2,
            Fx.Store.GetBlock(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlock(Fx.Block3.BlockHash));
        Assert.Equal(Fx.Block1.Height, Fx.Store.GetBlockHeight(Fx.Block1.BlockHash));
        Assert.Equal(Fx.Block2.Height, Fx.Store.GetBlockHeight(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHeight(Fx.Block3.BlockHash));
        Assert.True(Fx.Store.ContainsBlock(Fx.Block1.BlockHash));
        Assert.True(Fx.Store.ContainsBlock(Fx.Block2.BlockHash));
        Assert.False(Fx.Store.ContainsBlock(Fx.Block3.BlockHash));

        Assert.True(Fx.Store.DeleteBlock(Fx.Block1.BlockHash));
        Assert.Equal(1, Fx.Store.CountBlocks());
        Assert.Equal(
            new HashSet<BlockHash> { Fx.Block2.BlockHash },
            [.. Fx.Store.IterateBlockHashes()]);
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlock(Fx.Block1.BlockHash));
        Assert.Equal(
            Fx.Block2,
            Fx.Store.GetBlock(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlock(Fx.Block3.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHeight(Fx.Block1.BlockHash));
        Assert.Equal(Fx.Block2.Height, Fx.Store.GetBlockHeight(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHeight(Fx.Block3.BlockHash));
        Assert.False(Fx.Store.ContainsBlock(Fx.Block1.BlockHash));
        Assert.True(Fx.Store.ContainsBlock(Fx.Block2.BlockHash));
        Assert.False(Fx.Store.ContainsBlock(Fx.Block3.BlockHash));
    }

    [Fact]
    public void TxExecution()
    {
        static void AssertTxExecutionEqual(TxExecution expected, TxExecution actual)
        {
            Assert.Equal(expected.Fail, actual.Fail);
            Assert.Equal(expected.TxId, actual.TxId);
            Assert.Equal(expected.BlockHash, actual.BlockHash);
            Assert.Equal(expected.InputState, actual.InputState);
            Assert.Equal(expected.OutputState, actual.OutputState);
            Assert.Equal(expected.ExceptionNames, actual.ExceptionNames);
        }

        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash1, Fx.TxId1));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash1, Fx.TxId2));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash2, Fx.TxId1));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash2, Fx.TxId2));

        var inputA = new TxExecution
        {
            BlockHash = Fx.Hash1,
            TxId = Fx.TxId1,
            InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            ExceptionNames = [],
        };
        Fx.Store.PutTxExecution(inputA);

        AssertTxExecutionEqual(inputA, Fx.Store.GetTxExecution(Fx.Hash1, Fx.TxId1));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash1, Fx.TxId2));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash2, Fx.TxId1));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash2, Fx.TxId2));

        var inputB = new TxExecution
        {
            BlockHash = Fx.Hash1,
            TxId = Fx.TxId2,
            InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            ExceptionNames = ["AnExceptionName"],
        };
        Fx.Store.PutTxExecution(inputB);

        AssertTxExecutionEqual(inputA, Fx.Store.GetTxExecution(Fx.Hash1, Fx.TxId1));
        AssertTxExecutionEqual(inputB, Fx.Store.GetTxExecution(Fx.Hash1, Fx.TxId2));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash2, Fx.TxId2));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash2, Fx.TxId1));

        var inputC = new TxExecution
        {
            BlockHash = Fx.Hash2,
            TxId = Fx.TxId1,
            InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            ExceptionNames = ["AnotherExceptionName", "YetAnotherExceptionName"],
        };
        Fx.Store.PutTxExecution(inputC);

        AssertTxExecutionEqual(inputA, Fx.Store.GetTxExecution(Fx.Hash1, Fx.TxId1));
        AssertTxExecutionEqual(inputB, Fx.Store.GetTxExecution(Fx.Hash1, Fx.TxId2));
        AssertTxExecutionEqual(inputC, Fx.Store.GetTxExecution(Fx.Hash2, Fx.TxId1));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTxExecution(Fx.Hash2, Fx.TxId2));
    }

    [Fact]
    public void TxIdBlockHashIndex()
    {
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId1));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId2));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId3));

        Fx.Store.PutTxIdBlockHashIndex(Fx.TxId1, Fx.Hash1);
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId2));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId3));

        Fx.Store.PutTxIdBlockHashIndex(Fx.TxId2, Fx.Hash2);
        Fx.Store.PutTxIdBlockHashIndex(Fx.TxId3, Fx.Hash3);

        Assert.True(Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId1).Equals(Fx.Hash1));
        Assert.True(Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId2).Equals(Fx.Hash2));
        Assert.True(Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId3).Equals(Fx.Hash3));

        Fx.Store.PutTxIdBlockHashIndex(Fx.TxId1, Fx.Hash3);
        Fx.Store.PutTxIdBlockHashIndex(Fx.TxId2, Fx.Hash3);
        Fx.Store.PutTxIdBlockHashIndex(Fx.TxId3, Fx.Hash1);
        Assert.Equal(2, Fx.Store.IterateTxIdBlockHashIndex(Fx.TxId1).Count());
        Assert.Equal(2, Fx.Store.IterateTxIdBlockHashIndex(Fx.TxId2).Count());
        Assert.Equal(2, Fx.Store.IterateTxIdBlockHashIndex(Fx.TxId3).Count());

        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId1, Fx.Hash1);
        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId2, Fx.Hash2);
        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId3, Fx.Hash3);

        Assert.True(Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId1).Equals(Fx.Hash3));
        Assert.True(Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId2).Equals(Fx.Hash3));
        Assert.True(Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId3).Equals(Fx.Hash1));

        Assert.Single(Fx.Store.IterateTxIdBlockHashIndex(Fx.TxId1));
        Assert.Single(Fx.Store.IterateTxIdBlockHashIndex(Fx.TxId2));
        Assert.Single(Fx.Store.IterateTxIdBlockHashIndex(Fx.TxId3));

        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId1, Fx.Hash1);
        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId2, Fx.Hash2);
        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId3, Fx.Hash3);

        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId1, Fx.Hash3);
        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId2, Fx.Hash3);
        Fx.Store.DeleteTxIdBlockHashIndex(Fx.TxId3, Fx.Hash1);

        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId1));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId2));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetFirstTxIdBlockHashIndex(Fx.TxId3));
    }

    [Fact]
    public void StoreTx()
    {
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTransaction(Fx.Transaction1.Id));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTransaction(Fx.Transaction2.Id));
        Assert.False(Fx.Store.ContainsTransaction(Fx.Transaction1.Id));
        Assert.False(Fx.Store.ContainsTransaction(Fx.Transaction2.Id));

        Fx.Store.PutTransaction(Fx.Transaction1);
        Assert.Equal(
            Fx.Transaction1,
            Fx.Store.GetTransaction(Fx.Transaction1.Id));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetTransaction(Fx.Transaction2.Id));
        Assert.True(Fx.Store.ContainsTransaction(Fx.Transaction1.Id));
        Assert.False(Fx.Store.ContainsTransaction(Fx.Transaction2.Id));

        Fx.Store.PutTransaction(Fx.Transaction2);
        Assert.Equal(
            Fx.Transaction1,
            Fx.Store.GetTransaction(Fx.Transaction1.Id));
        Assert.Equal(
            Fx.Transaction2,
            Fx.Store.GetTransaction(Fx.Transaction2.Id));
        Assert.True(Fx.Store.ContainsTransaction(Fx.Transaction1.Id));
        Assert.True(Fx.Store.ContainsTransaction(Fx.Transaction2.Id));

        Assert.Equal(
            Fx.Transaction2,
            Fx.Store.GetTransaction(Fx.Transaction2.Id));
        Assert.True(Fx.Store.ContainsTransaction(Fx.Transaction2.Id));
    }

    [Fact]
    public void StoreIndex()
    {
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.CountIndex(Fx.StoreChainId));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHash(Fx.StoreChainId, 0));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHash(Fx.StoreChainId, -1));

        Assert.Equal(0, Fx.Store.AppendIndex(Fx.StoreChainId, Fx.Hash1));
        Assert.Equal(1, Fx.Store.CountIndex(Fx.StoreChainId));
        Assert.Equal(
            [Fx.Hash1],
            Fx.Store.IterateIndexes(Fx.StoreChainId));
        Assert.Equal(Fx.Hash1, Fx.Store.GetBlockHash(Fx.StoreChainId, 0));
        Assert.Equal(Fx.Hash1, Fx.Store.GetBlockHash(Fx.StoreChainId, -1));

        Assert.Equal(1, Fx.Store.AppendIndex(Fx.StoreChainId, Fx.Hash2));
        Assert.Equal(2, Fx.Store.CountIndex(Fx.StoreChainId));
        Assert.Equal(
            new List<BlockHash> { Fx.Hash1, Fx.Hash2 },
            Fx.Store.IterateIndexes(Fx.StoreChainId));
        Assert.Equal(Fx.Hash1, Fx.Store.GetBlockHash(Fx.StoreChainId, 0));
        Assert.Equal(Fx.Hash2, Fx.Store.GetBlockHash(Fx.StoreChainId, 1));
        Assert.Equal(Fx.Hash2, Fx.Store.GetBlockHash(Fx.StoreChainId, -1));
        Assert.Equal(Fx.Hash1, Fx.Store.GetBlockHash(Fx.StoreChainId, -2));
    }

    [Fact]
    public void IterateIndexes()
    {
        var ns = Fx.StoreChainId;
        var store = Fx.Store;

        store.AppendIndex(ns, Fx.Hash1);
        store.AppendIndex(ns, Fx.Hash2);
        store.AppendIndex(ns, Fx.Hash3);

        var indexes = store.IterateIndexes(ns).ToArray();
        Assert.Equal(new[] { Fx.Hash1, Fx.Hash2, Fx.Hash3 }, indexes);

        indexes = [.. store.IterateIndexes(ns, 1)];
        Assert.Equal(new[] { Fx.Hash2, Fx.Hash3 }, indexes);

        indexes = [.. store.IterateIndexes(ns, 2)];
        Assert.Equal(new[] { Fx.Hash3 }, indexes);

        indexes = [.. store.IterateIndexes(ns, 3)];
        Assert.Equal([], indexes);

        indexes = [.. store.IterateIndexes(ns, 4)];
        Assert.Equal([], indexes);

        indexes = [.. store.IterateIndexes(ns, limit: 0)];
        Assert.Equal([], indexes);

        indexes = [.. store.IterateIndexes(ns, limit: 1)];
        Assert.Equal(new[] { Fx.Hash1 }, indexes);

        indexes = [.. store.IterateIndexes(ns, limit: 2)];
        Assert.Equal(new[] { Fx.Hash1, Fx.Hash2 }, indexes);

        indexes = [.. store.IterateIndexes(ns, limit: 3)];
        Assert.Equal(new[] { Fx.Hash1, Fx.Hash2, Fx.Hash3 }, indexes);

        indexes = [.. store.IterateIndexes(ns, limit: 4)];
        Assert.Equal(new[] { Fx.Hash1, Fx.Hash2, Fx.Hash3 }, indexes);

        indexes = [.. store.IterateIndexes(ns, 1, 1)];
        Assert.Equal(new[] { Fx.Hash2 }, indexes);
    }

    [Fact]
    public void TxNonce()
    {
        Assert.Equal(0, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction1.Signer));
        Assert.Equal(0, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction2.Signer));

        Fx.Store.IncreaseTxNonce(Fx.StoreChainId, Fx.Transaction1.Signer);
        Assert.Equal(1, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction1.Signer));
        Assert.Equal(0, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 1,
            },
            Fx.Store.ListTxNonces(Fx.StoreChainId).ToDictionary(p => p.Key, p => p.Value));

        Fx.Store.IncreaseTxNonce(Fx.StoreChainId, Fx.Transaction2.Signer, 5);
        Assert.Equal(1, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction1.Signer));
        Assert.Equal(5, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 1,
                [Fx.Transaction2.Signer] = 5,
            },
            Fx.Store.ListTxNonces(Fx.StoreChainId).ToDictionary(p => p.Key, p => p.Value));

        Fx.Store.IncreaseTxNonce(Fx.StoreChainId, Fx.Transaction1.Signer, 2);
        Assert.Equal(3, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction1.Signer));
        Assert.Equal(5, Fx.Store.GetTxNonce(Fx.StoreChainId, Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 3,
                [Fx.Transaction2.Signer] = 5,
            },
            Fx.Store.ListTxNonces(Fx.StoreChainId).ToDictionary(p => p.Key, p => p.Value));
    }

    [Fact]
    public void ListTxNonces()
    {
        var chainId1 = Guid.NewGuid();
        var chainId2 = Guid.NewGuid();

        Address address1 = Fx.Address1;
        Address address2 = Fx.Address2;

        Assert.Empty(Fx.Store.ListTxNonces(chainId1));
        Assert.Empty(Fx.Store.ListTxNonces(chainId2));

        Fx.Store.IncreaseTxNonce(chainId1, address1);
        Assert.Equal(
            new Dictionary<Address, long> { [address1] = 1, },
            Fx.Store.ListTxNonces(chainId1));

        Fx.Store.IncreaseTxNonce(chainId2, address2);
        Assert.Equal(
            new Dictionary<Address, long> { [address2] = 1, },
            Fx.Store.ListTxNonces(chainId2));

        Fx.Store.IncreaseTxNonce(chainId1, address1);
        Fx.Store.IncreaseTxNonce(chainId1, address2);
        Assert.Equal(
            ImmutableSortedDictionary<Address, long>.Empty
                .Add(address1, 2)
                .Add(address2, 1),
            Fx.Store.ListTxNonces(chainId1).ToImmutableSortedDictionary());

        Fx.Store.IncreaseTxNonce(chainId2, address1);
        Fx.Store.IncreaseTxNonce(chainId2, address2);
        Assert.Equal(
            ImmutableSortedDictionary<Address, long>.Empty
                .Add(address1, 1)
                .Add(address2, 2),
            Fx.Store.ListTxNonces(chainId2).ToImmutableSortedDictionary());
    }

    [Fact]
    public void IndexBlockHashReturnNull()
    {
        Fx.Store.PutBlock(Fx.Block1);
        Fx.Store.AppendIndex(Fx.StoreChainId, Fx.Block1.BlockHash);
        Assert.Equal(1, Fx.Store.CountIndex(Fx.StoreChainId));
        Assert.Throws<KeyNotFoundException>(() => Fx.Store.GetBlockHash(Fx.StoreChainId, 2));
    }

    [Fact]
    public void ContainsBlockWithoutCache()
    {
        Fx.Store.PutBlock(Fx.Block1);
        Fx.Store.PutBlock(Fx.Block2);
        Fx.Store.PutBlock(Fx.Block3);

        Assert.True(Fx.Store.ContainsBlock(Fx.Block1.BlockHash));
        Assert.True(Fx.Store.ContainsBlock(Fx.Block2.BlockHash));
        Assert.True(Fx.Store.ContainsBlock(Fx.Block3.BlockHash));
    }

    [Fact]
    public void ContainsTransactionWithoutCache()
    {
        Fx.Store.PutTransaction(Fx.Transaction1);
        Fx.Store.PutTransaction(Fx.Transaction2);
        Fx.Store.PutTransaction(Fx.Transaction3);

        Assert.True(Fx.Store.ContainsTransaction(Fx.Transaction1.Id));
        Assert.True(Fx.Store.ContainsTransaction(Fx.Transaction2.Id));
        Assert.True(Fx.Store.ContainsTransaction(Fx.Transaction3.Id));
    }

    [Fact]
    public void TxAtomicity()
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
            return Transaction.Create(
                txNonce,
                key,
                default,
                new[] { action }.ToBytecodes(),
                null,
                0L,
                DateTimeOffset.UtcNow);
        }

        const int taskCount = 5;
        const int txCount = 30;
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
                PrivateKey key = new PrivateKey();
                var random = new System.Random();
                var md5 = MD5.Create();
                Transaction tx;
                for (int j = 0; j < 50; j++)
                {
                    Fx.Store.PutTransaction(commonTx);
                }

                for (int j = 0; j < txCount; j++)
                {
                    tx = MakeTx(random, md5, key, j + 1);
                    Fx.Store.PutTransaction(tx);
                }
            });
            task.Start();
            tasks[i] = task;
        }

        try
        {
            Task.WaitAll(tasks);
        }
        catch (AggregateException e)
        {
            foreach (Exception innerException in e.InnerExceptions)
            {
                TestOutputHelper.WriteLine(innerException.ToString());
            }

            throw;
        }
    }

    [Fact]
    public void ForkBlockIndex()
    {
        Libplanet.Store.Store store = Fx.Store;
        Guid chainA = Guid.NewGuid();
        Guid chainB = Guid.NewGuid();
        Guid chainC = Guid.NewGuid();

        // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
        // actual block...
        store.PutBlock(Fx.GenesisBlock);
        store.PutBlock(Fx.Block1);
        store.PutBlock(Fx.Block2);
        store.PutBlock(Fx.Block3);

        store.AppendIndex(chainA, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(chainB, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(chainC, Fx.GenesisBlock.BlockHash);

        store.AppendIndex(chainA, Fx.Block1.BlockHash);
        store.ForkBlockIndexes(chainA, chainB, Fx.Block1.BlockHash);
        store.AppendIndex(chainB, Fx.Block2.BlockHash);
        store.AppendIndex(chainB, Fx.Block3.BlockHash);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
            ],
            store.IterateIndexes(chainA));
        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
            ],
            store.IterateIndexes(chainB));

        store.ForkBlockIndexes(chainB, chainC, Fx.Block3.BlockHash);
        store.AppendIndex(chainC, Fx.Block4.BlockHash);
        store.AppendIndex(chainC, Fx.Block5.BlockHash);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
            ],
            store.IterateIndexes(chainA));
        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
            ],
            store.IterateIndexes(chainB));
        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            store.IterateIndexes(chainC));

        Assert.Equal(
            [
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            store.IterateIndexes(chainC, offset: 1));

        Assert.Equal(
            [
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            store.IterateIndexes(chainC, offset: 2));

        Assert.Equal(
            [
                Fx.Block3.BlockHash,
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            store.IterateIndexes(chainC, offset: 3));

        Assert.Equal(
            [
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            store.IterateIndexes(chainC, offset: 4));

        Assert.Equal(
            [
                Fx.Block5.BlockHash,
            ],
            store.IterateIndexes(chainC, offset: 5));

        Assert.Equal(
            Array.Empty<BlockHash>(),
            store.IterateIndexes(chainC, offset: 6));

        Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHash(chainA, 1));
        Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHash(chainB, 1));
        Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHash(chainC, 1));
        Assert.Equal(Fx.Block2.BlockHash, store.GetBlockHash(chainB, 2));
        Assert.Equal(Fx.Block2.BlockHash, store.GetBlockHash(chainC, 2));
        Assert.Equal(Fx.Block3.BlockHash, store.GetBlockHash(chainB, 3));
        Assert.Equal(Fx.Block3.BlockHash, store.GetBlockHash(chainC, 3));
        Assert.Equal(Fx.Block4.BlockHash, store.GetBlockHash(chainC, 4));
        Assert.Equal(Fx.Block5.BlockHash, store.GetBlockHash(chainC, 5));
    }

    [Fact]
    public void ForkWithBranch()
    {
        Libplanet.Store.Store store = Fx.Store;
        Guid chainA = Guid.NewGuid();
        Guid chainB = Guid.NewGuid();

        // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
        // actual block...
        Block anotherBlock3 = ProposeNextBlock(
            Fx.Block2,
            Fx.Proposer,
            lastCommit: CreateBlockCommit(Fx.Block2.BlockHash, 2, 0));
        store.PutBlock(Fx.GenesisBlock);
        store.PutBlock(Fx.Block1);
        store.PutBlock(Fx.Block2);
        store.PutBlock(Fx.Block3);
        store.PutBlock(anotherBlock3);

        store.AppendIndex(chainA, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(chainA, Fx.Block1.BlockHash);
        store.AppendIndex(chainA, Fx.Block2.BlockHash);
        store.AppendIndex(chainA, Fx.Block3.BlockHash);

        store.ForkBlockIndexes(chainA, chainB, Fx.Block2.BlockHash);
        store.AppendIndex(chainB, anotherBlock3.BlockHash);

        Assert.Equal(
            [
                Fx.Block2.BlockHash,
                anotherBlock3.BlockHash,
            ],
            store.IterateIndexes(chainB, 2, 2));
        Assert.Equal(
            [
                Fx.Block2.BlockHash,
                anotherBlock3.BlockHash,
            ],
            store.IterateIndexes(chainB, 2));

        Assert.Equal(
            [
                anotherBlock3.BlockHash,
            ],
            store.IterateIndexes(chainB, 3, 1));

        Assert.Equal(
            [
                anotherBlock3.BlockHash,
            ],
            store.IterateIndexes(chainB, 3));
    }

    [Fact]
    public void Copy()
    {
        using (StoreFixture fx = FxConstructor())
        using (StoreFixture fx2 = FxConstructor())
        {
            Libplanet.Store.Store s1 = fx.Store, s2 = fx2.Store;
            var preEval = ProposeGenesis(proposer: GenesisProposer.PublicKey);
            var genesis = preEval.Sign(
                GenesisProposer,
                default);
            var blocks = BlockChain.Create(genesis, fx.Options);

            // FIXME: Need to add more complex blocks/transactions.
            var key = new PrivateKey();
            var block = blocks.ProposeBlock(key);
            blocks.Append(block, CreateBlockCommit(block));
            block = blocks.ProposeBlock(key, CreateBlockCommit(blocks.Tip));
            blocks.Append(block, CreateBlockCommit(block));
            block = blocks.ProposeBlock(key, CreateBlockCommit(blocks.Tip));
            blocks.Append(block, CreateBlockCommit(block));

            s1.Copy(to: Fx.Store);
            Fx.Store.Copy(to: s2);

            Assert.Equal(s1.ListChainIds().ToHashSet(), [.. s2.ListChainIds()]);
            Assert.Equal(s1.ChainId, s2.ChainId);
            foreach (Guid chainId in s1.ListChainIds())
            {
                Assert.Equal(s1.IterateIndexes(chainId), s2.IterateIndexes(chainId));
                foreach (BlockHash blockHash in s1.IterateIndexes(chainId))
                {
                    Assert.Equal(s1.GetBlock(blockHash), s2.GetBlock(blockHash));
                }
            }

            // ArgumentException is thrown if the destination store is not empty.
            Assert.Throws<ArgumentException>(() => Fx.Store.Copy(fx2.Store));
        }
    }

    [Fact]
    public void GetBlock()
    {
        using (StoreFixture fx = FxConstructor())
        {
            Block genesisBlock = fx.GenesisBlock;
            Block block = ProposeNextBlock(
                genesisBlock,
                proposer: fx.Proposer);

            fx.Store.PutBlock(block);
            Block storedBlock =
                fx.Store.GetBlock(block.BlockHash);

            Assert.Equal(block, storedBlock);
        }
    }

    [Fact]
    public void GetBlockCommit()
    {
        using (StoreFixture fx = FxConstructor())
        {
            // Commits with votes
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
                ValidatorPublicKey = validator.PublicKey,
                ValidatorPower = BigInteger.One,
                Flag = VoteFlag.PreCommit,
            }.Sign(validator)).ToImmutableArray();

            BlockCommit commit = new BlockCommit
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Votes = votes,
            };
            fx.Store.PutBlockCommit(commit);
            BlockCommit storedCommitVotes =
                fx.Store.GetBlockCommit(commit.BlockHash);

            Assert.Equal(commit, storedCommitVotes);
        }
    }

    [Fact]
    public void GetBlockCommitIndices()
    {
        using (StoreFixture fx = FxConstructor())
        {
            var votesOne = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = fx.Block1.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = fx.Proposer.PublicKey,
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
                    ValidatorPublicKey = fx.Proposer.PublicKey,
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

            foreach (var blockCommit in blockCommits)
            {
                fx.Store.PutBlockCommit(blockCommit);
            }

            IEnumerable<BlockHash> indices = fx.Store.GetBlockCommitHashes();

            HashSet<long> indicesFromOperation = [.. indices.Select(hash => fx.Store.GetBlockCommit(hash).Height)];
            HashSet<long> expectedIndices = new HashSet<long>() { 1, 2 };

            Assert.Equal(indicesFromOperation, expectedIndices);
        }
    }

    [Fact]
    public void DeleteLastCommit()
    {
        using (StoreFixture fx = FxConstructor())
        {
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
                        ValidatorPublicKey = validatorPrivateKey.PublicKey,
                        ValidatorPower = BigInteger.One,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(validatorPrivateKey)
                ],
            };

            fx.Store.PutBlockCommit(blockCommit);
            Assert.Equal(blockCommit, fx.Store.GetBlockCommit(blockCommit.BlockHash));

            fx.Store.DeleteBlockCommit(blockCommit.BlockHash);
            Assert.Throws<KeyNotFoundException>(() => fx.Store.GetBlockCommit(blockCommit.BlockHash));
        }
    }

    [Fact]
    public void IteratePendingEvidenceIds()
    {
        using (StoreFixture fx = FxConstructor())
        {
            var signer = TestUtils.ValidatorPrivateKeys[0];
            var duplicateVoteOne = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = fx.Block1.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = signer.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(signer))
                .Add(new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = fx.Block2.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = signer.PublicKey,
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
                    ValidatorPublicKey = signer.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(signer))
                .Add(new VoteMetadata
                {
                    Height = 2,
                    Round = 0,
                    BlockHash = fx.Block3.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = signer.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(signer));

            EvidenceBase[] evidences =
            [
                DuplicateVoteEvidence.Create(duplicateVoteOne[0], duplicateVoteOne[1], TestUtils.Validators),
                DuplicateVoteEvidence.Create(duplicateVoteTwo[0], duplicateVoteTwo[1], TestUtils.Validators),
            ];

            foreach (var evidence in evidences)
            {
                fx.Store.PendingEvidences.Add(evidence);
            }

            IEnumerable<EvidenceId> ids = fx.Store.PendingEvidences.Keys;
            Assert.Equal(evidences.Select(e => e.Id).ToHashSet(), [.. ids]);
        }
    }

    [Fact]
    public void ManipulatePendingEvidence()
    {
        using (StoreFixture fx = FxConstructor())
        {
            var signer = TestUtils.ValidatorPrivateKeys[0];
            var duplicateVote = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = fx.Block1.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = signer.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(signer))
                .Add(new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = fx.Block2.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = signer.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(signer));
            var evidence = DuplicateVoteEvidence.Create(duplicateVote[0], duplicateVote[1], TestUtils.Validators);

            Assert.DoesNotContain(evidence.Id, fx.Store.PendingEvidences.Keys);

            fx.Store.PendingEvidences.Add(evidence);
            EvidenceBase storedEvidence = fx.Store.PendingEvidences[evidence.Id];

            Assert.Equal(evidence, storedEvidence);
            Assert.Contains(evidence.Id, fx.Store.PendingEvidences.Keys);

            fx.Store.PendingEvidences.Remove(evidence.Id);
            Assert.DoesNotContain(evidence.Id, fx.Store.PendingEvidences.Keys);
        }
    }

    [Fact]
    public void ManipulateCommittedEvidence()
    {
        using (StoreFixture fx = FxConstructor())
        {
            var signer = TestUtils.ValidatorPrivateKeys[0];
            var duplicateVote = ImmutableArray<Vote>.Empty
                .Add(new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = fx.Block1.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = signer.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(signer))
                .Add(new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = fx.Block2.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    ValidatorPublicKey = signer.PublicKey,
                    ValidatorPower = BigInteger.One,
                    Flag = VoteFlag.PreCommit,
                }.Sign(signer));
            var evidence = DuplicateVoteEvidence.Create(duplicateVote[0], duplicateVote[1], TestUtils.Validators);

            Assert.DoesNotContain(evidence.Id, fx.Store.CommittedEvidences.Keys);

            // fx.Store.PutCommittedEvidence(evidence);
            EvidenceBase storedEvidence = fx.Store.CommittedEvidences[evidence.Id];

            Assert.Equal(evidence, storedEvidence);
            Assert.Contains(evidence.Id, fx.Store.CommittedEvidences.Keys);

            // fx.Store.DeleteCommittedEvidence(evidence.Id);
            Assert.DoesNotContain(evidence.Id, fx.Store.CommittedEvidences.Keys);
        }
    }

    [Fact]
    public void ForkTxNonces()
    {
        Libplanet.Store.Store store = Fx.Store;
        Guid sourceChainId = Guid.NewGuid();
        Guid destinationChainId = Guid.NewGuid();
        store.IncreaseTxNonce(sourceChainId, Fx.Address1, 1);
        store.IncreaseTxNonce(sourceChainId, Fx.Address2, 2);
        store.IncreaseTxNonce(sourceChainId, Fx.Address3, 3);

        store.ForkTxNonces(sourceChainId, destinationChainId);

        Assert.Equal(1, store.GetTxNonce(destinationChainId, Fx.Address1));
        Assert.Equal(2, store.GetTxNonce(destinationChainId, Fx.Address2));
        Assert.Equal(3, store.GetTxNonce(destinationChainId, Fx.Address3));

        store.IncreaseTxNonce(sourceChainId, Fx.Address1, 1);
        Assert.Equal(2, store.GetTxNonce(sourceChainId, Fx.Address1));
        Assert.Equal(1, store.GetTxNonce(destinationChainId, Fx.Address1));
    }

    [Fact]
    public void PruneOutdatedChains()
    {
        Libplanet.Store.Store store = Fx.Store;
        store.PutBlock(Fx.GenesisBlock);
        store.PutBlock(Fx.Block1);
        store.PutBlock(Fx.Block2);
        store.PutBlock(Fx.Block3);

        Guid cid1 = Guid.NewGuid();
        store.AppendIndex(cid1, Fx.GenesisBlock.BlockHash);
        store.AppendIndex(cid1, Fx.Block1.BlockHash);
        store.AppendIndex(cid1, Fx.Block2.BlockHash);
        Assert.Single(store.ListChainIds());
        Assert.Equal(
            [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
            store.IterateIndexes(cid1, 0, null));

        Guid cid2 = Guid.NewGuid();
        store.ForkBlockIndexes(cid1, cid2, Fx.Block1.BlockHash);
        store.AppendIndex(cid2, Fx.Block2.BlockHash);
        store.AppendIndex(cid2, Fx.Block3.BlockHash);
        Assert.Equal(2, store.ListChainIds().Count());
        Assert.Equal(
            [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash, Fx.Block3.BlockHash],
            store.IterateIndexes(cid2, 0, null));

        Guid cid3 = Guid.NewGuid();
        store.ForkBlockIndexes(cid1, cid3, Fx.Block2.BlockHash);
        Assert.Equal(3, store.ListChainIds().Count());
        Assert.Equal(
            [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
            store.IterateIndexes(cid3, 0, null));

        Assert.Throws<InvalidOperationException>(() => store.PruneOutdatedChains());
        store.PruneOutdatedChains(true);
        store.ChainId = cid3;
        store.PruneOutdatedChains();
        Assert.Single(store.ListChainIds());
        Assert.Equal(
            [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
            store.IterateIndexes(cid3, 0, null));
        Assert.Equal(3, store.CountIndex(cid3));
    }

    [Fact]
    public void IdempotentDispose()
    {
#pragma warning disable S3966 // Objects should not be disposed more than once
        Fx.Store?.Dispose();
        Fx.Store?.Dispose();
#pragma warning restore S3966 // Objects should not be disposed more than once
    }

    [Model(Version = 1)]
    private sealed record class AtomicityTestAction : ActionBase, IEquatable<AtomicityTestAction>
    {
        [Property(0)]
        public ImmutableArray<byte> ArbitraryBytes { get; set; }

        [Property(1)]
        public ImmutableArray<byte> Md5Digest { get; set; }

        public override int GetHashCode() => ModelUtility.GetHashCode(this);

        public bool Equals(AtomicityTestAction? other) => ModelUtility.Equals(this, other);

        protected override void OnExecute(IWorldContext world, IActionContext context)
        {
        }
    }
}
