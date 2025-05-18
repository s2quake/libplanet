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
        var store = Fx.Store;
        var chainId = Fx.StoreChainId;
        var chain = store.Chains.GetOrAdd(chainId);
        Assert.Single(store.Chains);

        store.BlockDigests.Add(Fx.Block1);
        chain.BlockHashes.Add(Fx.Block1);
        Assert.Equal([chainId], [.. store.Chains.Keys]);

        var arbitraryGuid = Guid.NewGuid();
        var arbitraryChain = store.Chains.GetOrAdd(arbitraryGuid);
        arbitraryChain.BlockHashes.Add(Fx.Block1);
        Assert.Equal([chainId, arbitraryGuid], store.Chains.Keys.ToImmutableSortedSet());
    }

    [Fact]
    public void ListChainIdAfterForkAndDelete()
    {
        var store = Fx.Store;
        var chainAId = Guid.NewGuid();
        var chainBId = Guid.NewGuid();
        var chainA = store.Chains.GetOrAdd(chainAId);
        var chainB = store.Chains.GetOrAdd(chainBId);

        store.BlockDigests.Add(Fx.GenesisBlock);
        store.BlockDigests.Add(Fx.Block1);
        store.BlockDigests.Add(Fx.Block2);

        chainA.BlockHashes.Add(Fx.GenesisBlock);
        chainA.BlockHashes.Add(Fx.Block1);
        chainB.ForkFrom(chainA, Fx.Block1.BlockHash);
        chainB.BlockHashes.Add(Fx.Block2);

        store.Chains.Remove(chainAId);

        Assert.Equal([chainBId], store.Chains.Keys);
    }

    [Fact]
    public void DeleteChainId()
    {
        var block1 = ProposeNextBlock(
            ProposeGenesisBlock(GenesisProposer),
            GenesisProposer,
            [Fx.Transaction1]);
        var store = Fx.Store;
        var chainId = Fx.StoreChainId;
        var chain = store.Chains.GetOrAdd(chainId);
        chain.BlockHashes.Add(block1);

        var arbitraryChainId = Guid.NewGuid();
        var arbitraryChain = store.Chains.GetOrAdd(arbitraryChainId);
        arbitraryChain.BlockHashes.Add(block1);
        chain.Nonces.Increase(Fx.Transaction1.Signer);

        store.Chains.Remove(chainId);
        chain.Dispose();

        Assert.Equal([arbitraryChainId], store.Chains.Keys);
        Assert.Throws<ObjectDisposedException>(() => chain.Nonces[Fx.Transaction1.Signer]);
    }

    [Fact]
    public void DeleteChainIdIsIdempotent()
    {
        var store = Fx.Store;
        Assert.Empty(store.Chains.Keys);
        Assert.False(store.Chains.Remove(Guid.NewGuid()));
        Assert.Empty(store.Chains.Keys);
    }

    [Fact]
    public void DeleteChainIdWithForks()
    {
        Skip.IfNot(
            Environment.GetEnvironmentVariable("XUNIT_UNITY_RUNNER") is null,
            "Flaky test : Libplanet.Blocks.InvalidBlockSignatureException");

        var store = Fx.Store;
        var chainAId = Guid.NewGuid();
        var chainBId = Guid.NewGuid();
        var chainCId = Guid.NewGuid();
        var chainA = store.Chains.GetOrAdd(chainAId);
        var chainB = store.Chains.GetOrAdd(chainBId);
        var chainC = store.Chains.GetOrAdd(chainCId);

        store.BlockDigests.Add(Fx.GenesisBlock);
        store.BlockDigests.Add(Fx.Block1);
        store.BlockDigests.Add(Fx.Block2);
        store.BlockDigests.Add(Fx.Block3);

        chainA.BlockHashes.Add(Fx.GenesisBlock);
        chainB.BlockHashes.Add(Fx.GenesisBlock);
        chainC.BlockHashes.Add(Fx.GenesisBlock);

        chainA.BlockHashes.Add(Fx.Block1);
        chainB.ForkFrom(chainA, Fx.Block1.BlockHash);
        chainB.BlockHashes.Add(Fx.Block2);
        chainC.ForkFrom(chainB, Fx.Block2.BlockHash);
        chainC.BlockHashes.Add(Fx.Block3);

        store.Chains.Remove(chainAId);
        chainA.Dispose();

        Assert.Empty(chainA.BlockHashes.IterateHeights());
        Assert.Throws<ObjectDisposedException>(() => chainA.BlockHashes[0]);
        Assert.Throws<ObjectDisposedException>(() => chainA.BlockHashes[1]);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
            ],
            chainB.BlockHashes.IterateHeights());
        Assert.Equal(Fx.GenesisBlock.BlockHash, chainB.BlockHashes[0]);
        Assert.Equal(Fx.Block1.BlockHash, chainB.BlockHashes[1]);
        Assert.Equal(Fx.Block2.BlockHash, chainB.BlockHashes[2]);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
            ],
            chainC.BlockHashes.IterateHeights());
        Assert.Equal(Fx.GenesisBlock.BlockHash, chainC.BlockHashes[0]);
        Assert.Equal(Fx.Block1.BlockHash, chainC.BlockHashes[1]);
        Assert.Equal(Fx.Block2.BlockHash, chainC.BlockHashes[2]);
        Assert.Equal(Fx.Block3.BlockHash, chainC.BlockHashes[3]);

        store.Chains.Remove(chainBId);
        chainB.Dispose();

        Assert.Empty(chainA.BlockHashes.IterateHeights());
        Assert.Throws<ObjectDisposedException>(() => chainA.BlockHashes[0]);
        Assert.Throws<ObjectDisposedException>(() => chainA.BlockHashes[1]);

        Assert.Empty(chainB.BlockHashes.IterateHeights());
        Assert.Throws<ObjectDisposedException>(() => chainB.BlockHashes[0]);
        Assert.Throws<ObjectDisposedException>(() => chainB.BlockHashes[1]);
        Assert.Throws<ObjectDisposedException>(() => chainB.BlockHashes[2]);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
            ],
            chainC.BlockHashes.IterateHeights());
        Assert.Equal(Fx.GenesisBlock.BlockHash, chainC.BlockHashes[0]);
        Assert.Equal(Fx.Block1.BlockHash, chainC.BlockHashes[1]);
        Assert.Equal(Fx.Block2.BlockHash, chainC.BlockHashes[2]);
        Assert.Equal(Fx.Block3.BlockHash, chainC.BlockHashes[3]);

        store.Chains.Remove(chainCId);
        chainC.Dispose();

        Assert.Empty(chainA.BlockHashes.IterateHeights());
        Assert.Empty(chainB.BlockHashes.IterateHeights());
        Assert.Empty(chainC.BlockHashes.IterateHeights());
        Assert.Throws<ObjectDisposedException>(() => chainC.BlockHashes[0]);
        Assert.Throws<ObjectDisposedException>(() => chainC.BlockHashes[1]);
        Assert.Throws<ObjectDisposedException>(() => chainC.BlockHashes[2]);
        Assert.Throws<ObjectDisposedException>(() => chainC.BlockHashes[3]);
    }

    [Fact]
    public void DeleteChainIdWithForksReverse()
    {
        var store = Fx.Store;
        var chainAId = Guid.NewGuid();
        var chainBId = Guid.NewGuid();
        var chainCId = Guid.NewGuid();
        var chainA = store.Chains.GetOrAdd(chainAId);
        var chainB = store.Chains.GetOrAdd(chainBId);
        var chainC = store.Chains.GetOrAdd(chainCId);

        // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
        // actual block...
        store.BlockDigests.Add(Fx.GenesisBlock);
        store.BlockDigests.Add(Fx.Block1);
        store.BlockDigests.Add(Fx.Block2);
        store.BlockDigests.Add(Fx.Block3);

        chainA.BlockHashes.Add(Fx.GenesisBlock);
        chainB.BlockHashes.Add(Fx.GenesisBlock);
        chainC.BlockHashes.Add(Fx.GenesisBlock);

        chainA.BlockHashes.Add(Fx.Block1);
        chainB.ForkFrom(chainA, Fx.Block1.BlockHash);
        chainB.BlockHashes.Add(Fx.Block2);
        chainC.ForkFrom(chainB, Fx.Block2.BlockHash);
        chainC.BlockHashes.Add(Fx.Block3);

        store.Chains.Remove(chainCId);
        chainC.Dispose();

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
            ],
            chainA.BlockHashes.IterateHeights());
        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
            ],
            chainB.BlockHashes.IterateHeights());
        Assert.Empty(chainC.BlockHashes.IterateHeights());

        store.Chains.Remove(chainBId);
        chainB.Dispose();

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
            ],
            chainA.BlockHashes.IterateHeights());
        Assert.Empty(chainB.BlockHashes.IterateHeights());
        Assert.Empty(chainC.BlockHashes.IterateHeights());

        store.Chains.Remove(chainAId);
        chainA.Dispose();
        Assert.Empty(chainA.BlockHashes.IterateHeights());
        Assert.Empty(chainB.BlockHashes.IterateHeights());
        Assert.Empty(chainC.BlockHashes.IterateHeights());
    }

    [Fact]
    public void ForkFromChainWithDeletion()
    {
        var store = Fx.Store;
        var chainAId = Guid.NewGuid();
        var chainBId = Guid.NewGuid();
        var chainCId = Guid.NewGuid();
        var chainA = store.Chains.GetOrAdd(chainAId);
        var chainB = store.Chains.GetOrAdd(chainBId);
        var chainC = store.Chains.GetOrAdd(chainCId);

        store.BlockDigests.Add(Fx.GenesisBlock);
        store.BlockDigests.Add(Fx.Block1);
        store.BlockDigests.Add(Fx.Block2);
        store.BlockDigests.Add(Fx.Block3);

        chainA.BlockHashes.Add(Fx.GenesisBlock);
        chainA.BlockHashes.Add(Fx.Block1);
        chainB.ForkFrom(chainA, Fx.Block1.BlockHash);
        store.Chains.Remove(chainAId);
        chainA.Dispose();

        chainC.ForkFrom(chainB, Fx.Block1.BlockHash);
        Assert.Equal(
            Fx.Block1.BlockHash,
            chainC.BlockHashes[Fx.Block1.Height]);
    }

    [Fact]
    public void CanonicalChainId()
    {
        var store = Fx.Store;
        Assert.Equal(Guid.Empty, store.ChainId);

        var aId = Guid.NewGuid();
        Assert.Throws<KeyNotFoundException>(() => store.ChainId = aId);
        var a = store.Chains.GetOrAdd(aId);
        store.ChainId = aId;
        Assert.Equal(aId, store.ChainId);
        Assert.Equal(a, store.Chain);

        var bId = Guid.NewGuid();
        Assert.Throws<KeyNotFoundException>(() => store.ChainId = bId);
        var b = store.Chains.GetOrAdd(bId);
        store.ChainId = bId;
        Assert.Equal(bId, store.ChainId);
        Assert.Equal(b, store.Chain);
    }

    [Fact]
    public void StoreBlock()
    {
        var store = Fx.Store;
        Assert.Empty(store.BlockDigests.Keys);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block1.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block2.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block3.BlockHash]);
        Assert.False(store.BlockDigests.Remove(Fx.Block1.BlockHash));
        Assert.False(store.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.False(store.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.False(store.BlockDigests.ContainsKey(Fx.Block3.BlockHash));

        store.BlockDigests.Add(Fx.Block1);
        Assert.Single(store.BlockDigests);
        Assert.Equal(
            [Fx.Block1.BlockHash],
            store.BlockDigests.Keys);
        Assert.Equal(
            Fx.Block1,
            store.GetBlock(Fx.Block1.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block2.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block3.BlockHash]);
        Assert.Equal(Fx.Block1.Height, store.BlockDigests[Fx.Block1.BlockHash].Height);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block2.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block3.BlockHash]);
        Assert.True(store.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.False(store.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.False(store.BlockDigests.ContainsKey(Fx.Block3.BlockHash));

        store.BlockDigests.Add(Fx.Block2);
        Assert.Equal(2, store.BlockDigests.Count);
        Assert.Equal(
            new HashSet<BlockHash> { Fx.Block1.BlockHash, Fx.Block2.BlockHash },
            [.. store.BlockDigests.Keys]);
        Assert.Equal(
            Fx.Block1,
            store.GetBlock(Fx.Block1.BlockHash));
        Assert.Equal(
            Fx.Block2,
            store.GetBlock(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block3.BlockHash]);
        Assert.Equal(Fx.Block1.Height, store.BlockDigests[Fx.Block1.BlockHash].Height);
        Assert.Equal(Fx.Block2.Height, store.BlockDigests[Fx.Block2.BlockHash].Height);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block3.BlockHash]);
        Assert.True(store.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.True(store.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.False(store.BlockDigests.ContainsKey(Fx.Block3.BlockHash));

        Assert.True(store.BlockDigests.Remove(Fx.Block1.BlockHash));
        Assert.Single(store.BlockDigests);
        Assert.Equal(
            [Fx.Block2.BlockHash],
            store.BlockDigests.Keys);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block1.BlockHash]);
        Assert.Equal(
            Fx.Block2,
            store.GetBlock(Fx.Block2.BlockHash));
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block3.BlockHash]);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block1.BlockHash]);
        Assert.Equal(Fx.Block2.Height, store.BlockDigests[Fx.Block2.BlockHash].Height);
        Assert.Throws<KeyNotFoundException>(() => store.BlockDigests[Fx.Block3.BlockHash]);
        Assert.False(store.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.True(store.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.False(store.BlockDigests.ContainsKey(Fx.Block3.BlockHash));
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

        var store = Fx.Store;

        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1, Fx.Hash1]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash1]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1, Fx.Hash2]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash2]);

        var inputA = new TxExecution
        {
            BlockHash = Fx.Hash1,
            TxId = Fx.TxId1,
            InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            ExceptionNames = [],
        };
        store.TxExecutions.Add(inputA);

        AssertTxExecutionEqual(inputA, store.TxExecutions[Fx.TxId1, Fx.Hash1]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash1]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1, Fx.Hash2]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash2]);

        var inputB = new TxExecution
        {
            BlockHash = Fx.Hash1,
            TxId = Fx.TxId2,
            InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            ExceptionNames = ["AnExceptionName"],
        };
        store.TxExecutions.Add(inputB);

        AssertTxExecutionEqual(inputA, store.TxExecutions[Fx.TxId1, Fx.Hash1]);
        AssertTxExecutionEqual(inputB, store.TxExecutions[Fx.TxId2, Fx.Hash1]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash2]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1, Fx.Hash2]);

        var inputC = new TxExecution
        {
            BlockHash = Fx.Hash2,
            TxId = Fx.TxId1,
            InputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            OutputState = new HashDigest<SHA256>(GetRandomBytes(HashDigest<SHA256>.Size)),
            ExceptionNames = ["AnotherExceptionName", "YetAnotherExceptionName"],
        };
        store.TxExecutions.Add(inputC);

        AssertTxExecutionEqual(inputA, store.TxExecutions[Fx.TxId1, Fx.Hash1]);
        AssertTxExecutionEqual(inputB, store.TxExecutions[Fx.TxId2, Fx.Hash1]);
        AssertTxExecutionEqual(inputC, store.TxExecutions[Fx.TxId1, Fx.Hash2]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2, Fx.Hash2]);
    }

    [Fact]
    public void TxIdBlockHashIndex()
    {
        var store = Fx.Store;
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId3]);

        store.TxExecutions.Add(new TxExecution
        {
            TxId = Fx.TxId1,
            BlockHash = Fx.Hash1,
        });
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId3]);

        store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId2, BlockHash = Fx.Hash2 });
        store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId3, BlockHash = Fx.Hash3 });

        Assert.Single(store.TxExecutions[Fx.TxId1].Select(item => item.BlockHash), Fx.Hash1);
        Assert.Single(store.TxExecutions[Fx.TxId2].Select(item => item.BlockHash), Fx.Hash2);
        Assert.Single(store.TxExecutions[Fx.TxId3].Select(item => item.BlockHash), Fx.Hash3);

        store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId1, BlockHash = Fx.Hash3 });
        store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId2, BlockHash = Fx.Hash3 });
        store.TxExecutions.Add(new TxExecution { TxId = Fx.TxId3, BlockHash = Fx.Hash1 });
        Assert.Equal(2, store.TxExecutions[Fx.TxId1].Length);
        Assert.Equal(2, store.TxExecutions[Fx.TxId2].Length);
        Assert.Equal(2, store.TxExecutions[Fx.TxId3].Length);

        Assert.True(store.TxExecutions.Remove(Fx.TxId1, Fx.Hash1));
        Assert.True(store.TxExecutions.Remove(Fx.TxId2, Fx.Hash2));
        Assert.True(store.TxExecutions.Remove(Fx.TxId3, Fx.Hash3));

        Assert.Single(store.TxExecutions[Fx.TxId1].Select(item => item.BlockHash), Fx.Hash3);
        Assert.Single(store.TxExecutions[Fx.TxId2].Select(item => item.BlockHash), Fx.Hash3);
        Assert.Single(store.TxExecutions[Fx.TxId3].Select(item => item.BlockHash), Fx.Hash1);

        Assert.False(store.TxExecutions.Remove(Fx.TxId1, Fx.Hash1));
        Assert.False(store.TxExecutions.Remove(Fx.TxId2, Fx.Hash2));
        Assert.False(store.TxExecutions.Remove(Fx.TxId3, Fx.Hash3));

        Assert.True(store.TxExecutions.Remove(Fx.TxId1, Fx.Hash3));
        Assert.True(store.TxExecutions.Remove(Fx.TxId2, Fx.Hash3));
        Assert.True(store.TxExecutions.Remove(Fx.TxId3, Fx.Hash1));

        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId1]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId2]);
        Assert.Throws<KeyNotFoundException>(() => store.TxExecutions[Fx.TxId3]);
    }

    [Fact]
    public void StoreTx()
    {
        var store = Fx.Store;
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
    public void StoreIndex()
    {
        var store = Fx.Store;
        var chain = store.Chains.GetOrAdd(Fx.StoreChainId);
        Assert.Equal(0, chain.Height);
        Assert.Throws<KeyNotFoundException>(() => chain.BlockHashes[0]);
        Assert.Throws<KeyNotFoundException>(() => chain.BlockHashes[^1]);

        chain.GenesisHeight = Fx.Block1.Height;
        chain.BlockHashes.Add(Fx.Block1);
        Assert.Equal(1, chain.Height);
        Assert.Equal(
            [Fx.Block1.BlockHash],
            chain.BlockHashes.IterateHeights());
        Assert.Equal(Fx.Block1.BlockHash, chain.BlockHashes[1]);
        Assert.Equal(Fx.Block1.BlockHash, chain.BlockHashes[^1]);

        chain.BlockHashes.Add(Fx.Block2);
        Assert.Equal(2, chain.Height);
        Assert.Equal(
            [Fx.Block1.BlockHash, Fx.Block2.BlockHash],
            chain.BlockHashes.IterateHeights());
        Assert.Equal(Fx.Block1.BlockHash, chain.BlockHashes[1]);
        Assert.Equal(Fx.Block2.BlockHash, chain.BlockHashes[2]);
        Assert.Equal(Fx.Block2.BlockHash, chain.BlockHashes[^1]);
        Assert.Equal(Fx.Block1.BlockHash, chain.BlockHashes[^2]);
    }

    [Fact]
    public void IterateHeights()
    {
        var store = Fx.Store;
        var chain = store.Chains.GetOrAdd(Fx.StoreChainId);

        chain.BlockHashes.Add(0, Fx.Hash1);
        chain.BlockHashes.Add(1, Fx.Hash2);
        chain.BlockHashes.Add(2, Fx.Hash3);

        Assert.Equal(
            [Fx.Hash1, Fx.Hash2, Fx.Hash3],
            chain.BlockHashes.IterateHeights());
        Assert.Equal(
            [Fx.Hash2, Fx.Hash3],
            chain.BlockHashes.IterateHeights(1));
        Assert.Equal(
            [Fx.Hash3],
            chain.BlockHashes.IterateHeights(2));
        Assert.Equal([], chain.BlockHashes.IterateHeights(3));
        Assert.Equal([], chain.BlockHashes.IterateHeights(4));
        Assert.Equal([], chain.BlockHashes.IterateHeights(limit: 0));
        Assert.Equal(
            [Fx.Hash1],
            chain.BlockHashes.IterateHeights(limit: 1));
        Assert.Equal(
            [Fx.Hash1, Fx.Hash2],
            chain.BlockHashes.IterateHeights(limit: 2));
        Assert.Equal(
            [Fx.Hash1, Fx.Hash2, Fx.Hash3],
            chain.BlockHashes.IterateHeights(limit: 3));
        Assert.Equal(
            [Fx.Hash1, Fx.Hash2, Fx.Hash3],
            chain.BlockHashes.IterateHeights(limit: 4));
        Assert.Equal(
            [Fx.Hash2],
            chain.BlockHashes.IterateHeights(1, 1));
    }

    [Fact]
    public void TxNonce()
    {
        var chain = Fx.Store.Chains.GetOrAdd(Fx.StoreChainId);
        Assert.Equal(0, chain.GetNonce(Fx.Transaction1.Signer));
        Assert.Equal(0, chain.GetNonce(Fx.Transaction2.Signer));

        chain.IncreaseNonce(Fx.Transaction1.Signer);
        Assert.Equal(1, chain.GetNonce(Fx.Transaction1.Signer));
        Assert.Equal(0, chain.GetNonce(Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 1,
            }.ToImmutableSortedDictionary(),
            chain.Nonces.ToImmutableSortedDictionary());

        chain.IncreaseNonce(Fx.Transaction2.Signer, 5);
        Assert.Equal(1, chain.GetNonce(Fx.Transaction1.Signer));
        Assert.Equal(5, chain.GetNonce(Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 1,
                [Fx.Transaction2.Signer] = 5,
            }.ToImmutableSortedDictionary(),
            chain.Nonces.ToImmutableSortedDictionary());

        chain.IncreaseNonce(Fx.Transaction1.Signer, 2);
        Assert.Equal(3, chain.GetNonce(Fx.Transaction1.Signer));
        Assert.Equal(5, chain.GetNonce(Fx.Transaction2.Signer));
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [Fx.Transaction1.Signer] = 3,
                [Fx.Transaction2.Signer] = 5,
            }.ToImmutableSortedDictionary(),
            chain.Nonces.ToImmutableSortedDictionary());
    }

    [Fact]
    public void ListTxNonces()
    {
        var store = Fx.Store;
        var chainId1 = Guid.NewGuid();
        var chainId2 = Guid.NewGuid();
        var chain1 = store.Chains.GetOrAdd(chainId1);
        var chain2 = store.Chains.GetOrAdd(chainId2);

        var address1 = Fx.Address1;
        var address2 = Fx.Address2;

        Assert.Empty(chain1.Nonces);
        Assert.Empty(chain2.Nonces);

        chain1.IncreaseNonce(address1);
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [address1] = 1,
            }.ToImmutableSortedDictionary(),
            chain1.Nonces.ToImmutableSortedDictionary());

        chain2.IncreaseNonce(address2);
        Assert.Equal(
            new Dictionary<Address, long>
            {
                [address2] = 1,
            }.ToImmutableSortedDictionary(),
            chain2.Nonces.ToImmutableSortedDictionary());

        chain1.IncreaseNonce(address1);
        chain1.IncreaseNonce(address2);
        Assert.Equal(
            ImmutableSortedDictionary<Address, long>.Empty
                .Add(address1, 2)
                .Add(address2, 1),
            chain1.Nonces.ToImmutableSortedDictionary());

        chain2.IncreaseNonce(address1);
        chain2.IncreaseNonce(address2);
        Assert.Equal(
            ImmutableSortedDictionary<Address, long>.Empty
                .Add(address1, 1)
                .Add(address2, 2),
            chain2.Nonces.ToImmutableSortedDictionary());
    }

    [Fact]
    public void IndexBlockHashReturnNull()
    {
        var store = Fx.Store;
        var chain = store.Chains.GetOrAdd(Fx.StoreChainId);
        store.BlockDigests.Add(Fx.Block1);
        chain.BlockHashes.Add(1, Fx.Block1.BlockHash);
        Assert.Equal(1, chain.Height);
        Assert.Throws<KeyNotFoundException>(() => chain.BlockHashes[2]);
    }

    [Fact]
    public void ContainsBlockWithoutCache()
    {
        var store = Fx.Store;
        store.BlockDigests.Add(Fx.Block1);
        store.BlockDigests.Add(Fx.Block2);
        store.BlockDigests.Add(Fx.Block3);

        Assert.True(store.BlockDigests.ContainsKey(Fx.Block1.BlockHash));
        Assert.True(store.BlockDigests.ContainsKey(Fx.Block2.BlockHash));
        Assert.True(store.BlockDigests.ContainsKey(Fx.Block3.BlockHash));
    }

    [Fact]
    public void ContainsTransactionWithoutCache()
    {
        var store = Fx.Store;
        store.PendingTransactions.Add(Fx.Transaction1);
        store.PendingTransactions.Add(Fx.Transaction2);
        store.PendingTransactions.Add(Fx.Transaction3);

        Assert.True(store.PendingTransactions.ContainsKey(Fx.Transaction1.Id));
        Assert.True(store.PendingTransactions.ContainsKey(Fx.Transaction2.Id));
        Assert.True(store.PendingTransactions.ContainsKey(Fx.Transaction3.Id));
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
        var store = Fx.Store;
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
        var store = Fx.Store;
        var chainIdA = Guid.NewGuid();
        var chainIdB = Guid.NewGuid();
        var chainIdC = Guid.NewGuid();
        var chainA = store.Chains.GetOrAdd(chainIdA);
        var chainB = store.Chains.GetOrAdd(chainIdB);
        var chainC = store.Chains.GetOrAdd(chainIdC);

        // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
        // actual block...
        store.BlockDigests.Add(Fx.GenesisBlock);
        store.BlockDigests.Add(Fx.Block1);
        store.BlockDigests.Add(Fx.Block2);
        store.BlockDigests.Add(Fx.Block3);

        chainA.BlockHashes.Add(Fx.GenesisBlock);
        chainB.BlockHashes.Add(Fx.GenesisBlock);
        chainC.BlockHashes.Add(Fx.GenesisBlock);

        chainA.BlockHashes.Add(Fx.Block1);
        chainB.ForkFrom(chainA, Fx.Block1.BlockHash);
        chainB.BlockHashes.Add(Fx.Block2);
        chainB.BlockHashes.Add(Fx.Block3);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
            ],
            chainA.BlockHashes.IterateHeights());
        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
            ],
            chainB.BlockHashes.IterateHeights());

        chainC.ForkFrom(chainB, Fx.Block3.BlockHash);
        chainC.BlockHashes.Add(Fx.Block4);
        chainC.BlockHashes.Add(Fx.Block5);

        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
            ],
            chainA.BlockHashes.IterateHeights());
        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
            ],
            chainB.BlockHashes.IterateHeights());
        Assert.Equal(
            [
                Fx.GenesisBlock.BlockHash,
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            chainC.BlockHashes.IterateHeights());

        Assert.Equal(
            [
                Fx.Block1.BlockHash,
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            chainC.BlockHashes.IterateHeights(height: 1));

        Assert.Equal(
            [
                Fx.Block2.BlockHash,
                Fx.Block3.BlockHash,
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            chainC.BlockHashes.IterateHeights(height: 2));

        Assert.Equal(
            [
                Fx.Block3.BlockHash,
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            chainC.BlockHashes.IterateHeights(height: 3));

        Assert.Equal(
            [
                Fx.Block4.BlockHash,
                Fx.Block5.BlockHash,
            ],
            chainC.BlockHashes.IterateHeights(height: 4));

        Assert.Equal(
            [
                Fx.Block5.BlockHash,
            ],
            chainC.BlockHashes.IterateHeights(height: 5));

        Assert.Equal(
            [],
            chainC.BlockHashes.IterateHeights(height: 6));

        Assert.Equal(Fx.Block1.BlockHash, chainA.BlockHashes[1]);
        Assert.Equal(Fx.Block1.BlockHash, chainB.BlockHashes[1]);
        Assert.Equal(Fx.Block1.BlockHash, chainC.BlockHashes[1]);
        Assert.Equal(Fx.Block2.BlockHash, chainB.BlockHashes[2]);
        Assert.Equal(Fx.Block2.BlockHash, chainC.BlockHashes[2]);
        Assert.Equal(Fx.Block3.BlockHash, chainB.BlockHashes[3]);
        Assert.Equal(Fx.Block3.BlockHash, chainC.BlockHashes[3]);
        Assert.Equal(Fx.Block4.BlockHash, chainC.BlockHashes[4]);
        Assert.Equal(Fx.Block5.BlockHash, chainC.BlockHashes[5]);
    }

    [Fact]
    public void ForkWithBranch()
    {
        var store = Fx.Store;
        var chainIdA = Guid.NewGuid();
        var chainIdB = Guid.NewGuid();
        var chainA = store.Chains.GetOrAdd(chainIdA);
        var chainB = store.Chains.GetOrAdd(chainIdB);

        // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
        // actual block...
        var anotherBlock3 = ProposeNextBlock(
            Fx.Block2,
            Fx.Proposer,
            lastCommit: CreateBlockCommit(Fx.Block2.BlockHash, 2, 0));
        store.BlockDigests.Add(Fx.GenesisBlock);
        store.BlockDigests.Add(Fx.Block1);
        store.BlockDigests.Add(Fx.Block2);
        store.BlockDigests.Add(Fx.Block3);
        store.BlockDigests.Add(anotherBlock3);

        chainA.BlockHashes.Add(Fx.GenesisBlock);
        chainA.BlockHashes.Add(Fx.Block1);
        chainA.BlockHashes.Add(Fx.Block2);
        chainA.BlockHashes.Add(Fx.Block3);

        chainB.ForkFrom(chainA, Fx.Block2.BlockHash);
        chainB.BlockHashes.Add(anotherBlock3);

        Assert.Equal(
            [
                Fx.Block2.BlockHash,
                anotherBlock3.BlockHash,
            ],
            chainB.BlockHashes.IterateHeights(2, 2));
        Assert.Equal(
            [
                Fx.Block2.BlockHash,
                anotherBlock3.BlockHash,
            ],
            chainB.BlockHashes.IterateHeights(2));
        Assert.Equal(
            [
                anotherBlock3.BlockHash,
            ],
            chainB.BlockHashes.IterateHeights(3, 1));
        Assert.Equal(
            [
                anotherBlock3.BlockHash,
            ],
            chainB.BlockHashes.IterateHeights(3));
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
    //     var blockChain = BlockChain.Create(genesis, fx1.Options);

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
    public void GetBlock()
    {
        using var fx = FxConstructor();
        var store = fx.Store;
        var genesisBlock = fx.GenesisBlock;
        var expectedBlock = ProposeNextBlock(genesisBlock, fx.Proposer);

        store.BlockDigests.Add(expectedBlock);
        var actualBlock = store.GetBlock(expectedBlock.BlockHash);

        Assert.Equal(expectedBlock, actualBlock);
    }

    [Fact]
    public void GetBlockCommit()
    {
        using var fx = FxConstructor();
        var store = fx.Store;
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

        fx.Store.BlockCommits.AddRange(blockCommits);

        var actualHeight = fx.Store.BlockCommits.Values.Select(item => item.Height).ToImmutableSortedSet();

        Assert.Equal([1, 2], actualHeight);
    }

    [Fact]
    public void DeleteLastCommit()
    {
        using var fx = FxConstructor();
        var store = fx.Store;
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
        var store = fx.Store;
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
        var store = Fx.Store;
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
        var store = fx.Store;
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
    public void ForkTxNonces()
    {
        var store = Fx.Store;
        var sourceChain = store.Chains.GetOrAdd(Guid.NewGuid());
        var destChain = store.Chains.GetOrAdd(Guid.NewGuid());
        sourceChain.Nonces.Increase(Fx.Address1, 1);
        sourceChain.Nonces.Increase(Fx.Address2, 2);
        sourceChain.Nonces.Increase(Fx.Address3, 3);

        destChain.Nonces.MergeFrom(sourceChain.Nonces);
        Assert.Equal(1, destChain.Nonces[Fx.Address1]);
        Assert.Equal(2, destChain.Nonces[Fx.Address2]);
        Assert.Equal(3, destChain.Nonces[Fx.Address3]);

        sourceChain.Nonces.Increase(Fx.Address1, 1);
        Assert.Equal(2, sourceChain.Nonces[Fx.Address1]);
        Assert.Equal(1, destChain.Nonces[Fx.Address1]);
    }

    [Fact]
    public void PruneOutdatedChains()
    {
        var store = Fx.Store;
        store.BlockDigests.Add(Fx.GenesisBlock);
        store.BlockDigests.Add(Fx.Block1);
        store.BlockDigests.Add(Fx.Block2);
        store.BlockDigests.Add(Fx.Block3);

        var chain1 = store.Chains.GetOrAdd(Guid.NewGuid());
        chain1.BlockHashes.Add(Fx.GenesisBlock);
        chain1.BlockHashes.Add(Fx.Block1);
        chain1.BlockHashes.Add(Fx.Block2);
        Assert.Single(store.Chains.Keys);
        Assert.Equal(
            [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
            chain1.BlockHashes.IterateHeights(0, null));

        var chain2 = store.Chains.GetOrAdd(Guid.NewGuid());
        chain2.ForkFrom(chain1, Fx.Block1.BlockHash);
        chain2.BlockHashes.Add(Fx.Block2);
        chain2.BlockHashes.Add(Fx.Block3);
        Assert.Equal(2, store.Chains.Keys.Count);
        Assert.Equal(
            [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash, Fx.Block3.BlockHash],
            chain2.BlockHashes.IterateHeights(0, null));

        var chain3 = store.Chains.GetOrAdd(Guid.NewGuid());
        chain3.ForkFrom(chain2, Fx.Block2.BlockHash);
        Assert.Equal(3, store.Chains.Keys.Count);
        Assert.Equal(
            [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
            chain3.BlockHashes.IterateHeights(0, null));

        var outdatedChains = store.Chains.Values
            .Where(x => x.Id != chain3.Id)
            .ToImmutableArray();
        store.ChainId = chain3.Id;
        store.Chains.RemoveRange(outdatedChains);
        foreach (var chain in outdatedChains)
        {
            chain.Dispose();
        }

        Assert.Single(store.Chains.Keys);
        Assert.Equal(
            [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
            chain3.BlockHashes.IterateHeights(0, null));
        Assert.Equal(3, chain3.BlockHashes.Count);
    }

    [Fact]
    public void IdempotentDispose()
    {
        var store = Fx.Store;
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
