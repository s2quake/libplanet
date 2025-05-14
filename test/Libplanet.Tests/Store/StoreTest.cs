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
        store.ChainId = aId;
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

        Assert.True(store.TxExecutions.Remove(Fx.TxId1, Fx.Hash1));
        Assert.True(store.TxExecutions.Remove(Fx.TxId2, Fx.Hash2));
        Assert.True(store.TxExecutions.Remove(Fx.TxId3, Fx.Hash3));

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
        Assert.Throws<KeyNotFoundException>(() => store.Transactions[Fx.Transaction1.Id]);
        Assert.Throws<KeyNotFoundException>(() => store.Transactions[Fx.Transaction2.Id]);
        Assert.False(store.Transactions.ContainsKey(Fx.Transaction1.Id));
        Assert.False(store.Transactions.ContainsKey(Fx.Transaction2.Id));

        store.Transactions.Add(Fx.Transaction1);
        Assert.Equal(
            Fx.Transaction1,
            store.Transactions[Fx.Transaction1.Id]);
        Assert.Throws<KeyNotFoundException>(() => store.Transactions[Fx.Transaction2.Id]);
        Assert.True(store.Transactions.ContainsKey(Fx.Transaction1.Id));
        Assert.False(store.Transactions.ContainsKey(Fx.Transaction2.Id));

        store.Transactions.Add(Fx.Transaction2);
        Assert.Equal(
            Fx.Transaction1,
            store.Transactions[Fx.Transaction1.Id]);
        Assert.Equal(
            Fx.Transaction2,
            store.Transactions[Fx.Transaction2.Id]);
        Assert.True(store.Transactions.ContainsKey(Fx.Transaction1.Id));
        Assert.True(store.Transactions.ContainsKey(Fx.Transaction2.Id));

        Assert.Equal(
            Fx.Transaction2,
            store.Transactions[Fx.Transaction2.Id]);
        Assert.True(store.Transactions.ContainsKey(Fx.Transaction2.Id));
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

    //     [Fact]
    //     public void TxNonce()
    //     {
    //         Assert.Equal(0, store.GetNonceCollection(Fx.StoreChainId)[Fx.Transaction1.Signer]);
    //         Assert.Equal(0, store.GetNonceCollection(Fx.StoreChainId)[Fx.Transaction2.Signer]);

    //         store.GetNonceCollection(Fx.StoreChainId).Increase(Fx.Transaction1.Signer);
    //         Assert.Equal(1, store.GetNonceCollection(Fx.StoreChainId)[Fx.Transaction1.Signer]);
    //         Assert.Equal(0, store.GetNonceCollection(Fx.StoreChainId)[Fx.Transaction2.Signer]);
    //         Assert.Equal(
    //             new Dictionary<Address, long>
    //             {
    //                 [Fx.Transaction1.Signer] = 1,
    //             },
    //             store.GetNonceCollection(Fx.StoreChainId).ToDictionary(p => p.Key, p => p.Value));

    //         store.GetNonceCollection(Fx.StoreChainId).Increase(Fx.Transaction2.Signer, 5);
    //         Assert.Equal(1, store.GetNonceCollection(Fx.StoreChainId)[Fx.Transaction1.Signer]);
    //         Assert.Equal(5, store.GetNonceCollection(Fx.StoreChainId)[Fx.Transaction2.Signer]);
    //         Assert.Equal(
    //             new Dictionary<Address, long>
    //             {
    //                 [Fx.Transaction1.Signer] = 1,
    //                 [Fx.Transaction2.Signer] = 5,
    //             },
    //             store.GetNonceCollection(Fx.StoreChainId).ToDictionary(p => p.Key, p => p.Value));

    //         store.GetNonceCollection(Fx.StoreChainId).Increase(Fx.Transaction1.Signer, 2);
    //         Assert.Equal(3, store.GetNonceCollection(Fx.StoreChainId)[Fx.Transaction1.Signer]);
    //         Assert.Equal(5, store.GetNonceCollection(Fx.StoreChainId)[Fx.Transaction2.Signer]);
    //         Assert.Equal(
    //             new Dictionary<Address, long>
    //             {
    //                 [Fx.Transaction1.Signer] = 3,
    //                 [Fx.Transaction2.Signer] = 5,
    //             },
    //             store.GetNonceCollection(Fx.StoreChainId).ToDictionary(p => p.Key, p => p.Value));
    //     }

    //     [Fact]
    //     public void ListTxNonces()
    //     {
    //         var chainId1 = Guid.NewGuid();
    //         var chainId2 = Guid.NewGuid();

    //         Address address1 = Fx.Address1;
    //         Address address2 = Fx.Address2;

    //         Assert.Empty(store.GetNonceCollection(chainId1));
    //         Assert.Empty(store.GetNonceCollection(chainId2));

    //         store.GetNonceCollection(chainId1).Increase(address1);
    //         Assert.Equal(
    //             new Dictionary<Address, long> { [address1] = 1, },
    //             store.GetNonceCollection(chainId1));

    //         store.GetNonceCollection(chainId2).Increase(address2);
    //         Assert.Equal(
    //             new Dictionary<Address, long> { [address2] = 1, },
    //             store.GetNonceCollection(chainId2));

    //         store.GetNonceCollection(chainId1).Increase(address1);
    //         store.GetNonceCollection(chainId1).Increase(address2);
    //         Assert.Equal(
    //             ImmutableSortedDictionary<Address, long>.Empty
    //                 .Add(address1, 2)
    //                 .Add(address2, 1),
    //             store.GetNonceCollection(chainId1).ToImmutableSortedDictionary());

    //         store.GetNonceCollection(chainId2).Increase(address1);
    //         store.GetNonceCollection(chainId2).Increase(address2);
    //         Assert.Equal(
    //             ImmutableSortedDictionary<Address, long>.Empty
    //                 .Add(address1, 1)
    //                 .Add(address2, 2),
    //             store.GetNonceCollection(chainId2).ToImmutableSortedDictionary());
    //     }

    //     [Fact]
    //     public void IndexBlockHashReturnNull()
    //     {
    //         store.BlockDigests.Add(Fx.Block1);
    //         // store.AppendIndex(Fx.StoreChainId, Fx.Block1.BlockHash);
    //         Assert.Equal(1, store.Chains[Fx.StoreChainId].Height);
    //         Assert.Throws<KeyNotFoundException>(() => store.GetBlockHashes(Fx.StoreChainId)[2]);
    //     }

    //     [Fact]
    //     public void ContainsBlockWithoutCache()
    //     {
    //         store.BlockDigests.Add(Fx.Block1);
    //         store.BlockDigests.Add(Fx.Block2);
    //         store.BlockDigests.Add(Fx.Block3);

    //         Assert.True(store.Blocks.ContainsKey(Fx.Block1.BlockHash));
    //         Assert.True(store.Blocks.ContainsKey(Fx.Block2.BlockHash));
    //         Assert.True(store.Blocks.ContainsKey(Fx.Block3.BlockHash));
    //     }

    //     [Fact]
    //     public void ContainsTransactionWithoutCache()
    //     {
    //         store.Transactions.Add(Fx.Transaction1);
    //         store.Transactions.Add(Fx.Transaction2);
    //         store.Transactions.Add(Fx.Transaction3);

    //         Assert.True(store.Transactions.ContainsKey(Fx.Transaction1.Id));
    //         Assert.True(store.Transactions.ContainsKey(Fx.Transaction2.Id));
    //         Assert.True(store.Transactions.ContainsKey(Fx.Transaction3.Id));
    //     }

    //     [Fact]
    //     public void TxAtomicity()
    //     {
    //         Transaction MakeTx(
    //             System.Random random,
    //             MD5 md5,
    //             PrivateKey key,
    //             int txNonce)
    //         {
    //             byte[] arbitraryBytes = new byte[20];
    //             random.NextBytes(arbitraryBytes);
    //             byte[] digest = md5.ComputeHash(arbitraryBytes);
    //             var action = new AtomicityTestAction
    //             {
    //                 ArbitraryBytes = [.. arbitraryBytes],
    //                 Md5Digest = [.. digest],
    //             };
    //             return Transaction.Create(
    //                 txNonce,
    //                 key,
    //                 default,
    //                 new[] { action }.ToBytecodes(),
    //                 null,
    //                 0L,
    //                 DateTimeOffset.UtcNow);
    //         }

    //         const int taskCount = 5;
    //         const int txCount = 30;
    //         var md5Hasher = MD5.Create();
    //         Transaction commonTx = MakeTx(
    //             new System.Random(),
    //             md5Hasher,
    //             new PrivateKey(),
    //             0);
    //         Task[] tasks = new Task[taskCount];
    //         for (int i = 0; i < taskCount; i++)
    //         {
    //             var task = new Task(() =>
    //             {
    //                 PrivateKey key = new PrivateKey();
    //                 var random = new System.Random();
    //                 var md5 = MD5.Create();
    //                 Transaction tx;
    //                 for (int j = 0; j < 50; j++)
    //                 {
    //                     store.Transactions.Add(commonTx);
    //                 }

    //                 for (int j = 0; j < txCount; j++)
    //                 {
    //                     tx = MakeTx(random, md5, key, j + 1);
    //                     store.Transactions.Add(tx);
    //                 }
    //             });
    //             task.Start();
    //             tasks[i] = task;
    //         }

    //         try
    //         {
    //             Task.WaitAll(tasks);
    //         }
    //         catch (AggregateException e)
    //         {
    //             foreach (Exception innerException in e.InnerExceptions)
    //             {
    //                 TestOutputHelper.WriteLine(innerException.ToString());
    //             }

    //             throw;
    //         }
    //     }

    //     [Fact]
    //     public void ForkBlockIndex()
    //     {
    //         Libplanet.Store.Store store = Fx.Store;
    //         Guid chainA = Guid.NewGuid();
    //         Guid chainB = Guid.NewGuid();
    //         Guid chainC = Guid.NewGuid();

    //         // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
    //         // actual block...
    //         store.BlockDigests.Add(Fx.GenesisBlock);
    //         store.BlockDigests.Add(Fx.Block1);
    //         store.BlockDigests.Add(Fx.Block2);
    //         store.BlockDigests.Add(Fx.Block3);

    //         // store.AppendIndex(chainA, Fx.GenesisBlock.BlockHash);
    //         // store.AppendIndex(chainB, Fx.GenesisBlock.BlockHash);
    //         // store.AppendIndex(chainC, Fx.GenesisBlock.BlockHash);

    //         // store.AppendIndex(chainA, Fx.Block1.BlockHash);
    //         store.ForkBlockIndexes(chainA, chainB, Fx.Block1.BlockHash);
    //         // store.AppendIndex(chainB, Fx.Block2.BlockHash);
    //         // store.AppendIndex(chainB, Fx.Block3.BlockHash);

    //         Assert.Equal(
    //             [
    //                 Fx.GenesisBlock.BlockHash,
    //                 Fx.Block1.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainA).IterateHeights());
    //         Assert.Equal(
    //             [
    //                 Fx.GenesisBlock.BlockHash,
    //                 Fx.Block1.BlockHash,
    //                 Fx.Block2.BlockHash,
    //                 Fx.Block3.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainB).IterateHeights());

    //         store.ForkBlockIndexes(chainB, chainC, Fx.Block3.BlockHash);
    //         // store.AppendIndex(chainC, Fx.Block4.BlockHash);
    //         // store.AppendIndex(chainC, Fx.Block5.BlockHash);

    //         Assert.Equal(
    //             [
    //                 Fx.GenesisBlock.BlockHash,
    //                 Fx.Block1.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainA).IterateHeights());
    //         Assert.Equal(
    //             [
    //                 Fx.GenesisBlock.BlockHash,
    //                 Fx.Block1.BlockHash,
    //                 Fx.Block2.BlockHash,
    //                 Fx.Block3.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainB).IterateHeights());
    //         Assert.Equal(
    //             [
    //                 Fx.GenesisBlock.BlockHash,
    //                 Fx.Block1.BlockHash,
    //                 Fx.Block2.BlockHash,
    //                 Fx.Block3.BlockHash,
    //                 Fx.Block4.BlockHash,
    //                 Fx.Block5.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainC).IterateHeights());

    //         Assert.Equal(
    //             [
    //                 Fx.Block1.BlockHash,
    //                 Fx.Block2.BlockHash,
    //                 Fx.Block3.BlockHash,
    //                 Fx.Block4.BlockHash,
    //                 Fx.Block5.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainC).IterateHeights(offset: 1));

    //         Assert.Equal(
    //             [
    //                 Fx.Block2.BlockHash,
    //                 Fx.Block3.BlockHash,
    //                 Fx.Block4.BlockHash,
    //                 Fx.Block5.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainC).IterateHeights(offset: 2));

    //         Assert.Equal(
    //             [
    //                 Fx.Block3.BlockHash,
    //                 Fx.Block4.BlockHash,
    //                 Fx.Block5.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainC).IterateHeights(offset: 3));

    //         Assert.Equal(
    //             [
    //                 Fx.Block4.BlockHash,
    //                 Fx.Block5.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainC).IterateHeights(offset: 4));

    //         Assert.Equal(
    //             [
    //                 Fx.Block5.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainC).IterateHeights(offset: 5));

    //         Assert.Equal(
    //             Array.Empty<BlockHash>(),
    //             store.GetBlockHashes(chainC).IterateHeights(offset: 6));

    //         Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHashes(chainA)[1]);
    //         Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHashes(chainB)[1]);
    //         Assert.Equal(Fx.Block1.BlockHash, store.GetBlockHashes(chainC)[1]);
    //         Assert.Equal(Fx.Block2.BlockHash, store.GetBlockHashes(chainB)[2]);
    //         Assert.Equal(Fx.Block2.BlockHash, store.GetBlockHashes(chainC)[2]);
    //         Assert.Equal(Fx.Block3.BlockHash, store.GetBlockHashes(chainB)[3]);
    //         Assert.Equal(Fx.Block3.BlockHash, store.GetBlockHashes(chainC)[3]);
    //         Assert.Equal(Fx.Block4.BlockHash, store.GetBlockHashes(chainC)[4]);
    //         Assert.Equal(Fx.Block5.BlockHash, store.GetBlockHashes(chainC)[5]);
    //     }

    //     [Fact]
    //     public void ForkWithBranch()
    //     {
    //         Libplanet.Store.Store store = Fx.Store;
    //         Guid chainA = Guid.NewGuid();
    //         Guid chainB = Guid.NewGuid();

    //         // We need `Block<T>`s because `Libplanet.Store.Store` can't retrieve index(long) by block hash without
    //         // actual block...
    //         Block anotherBlock3 = ProposeNextBlock(
    //             Fx.Block2,
    //             Fx.Proposer,
    //             lastCommit: CreateBlockCommit(Fx.Block2.BlockHash, 2, 0));
    //         store.BlockDigests.Add(Fx.GenesisBlock);
    //         store.BlockDigests.Add(Fx.Block1);
    //         store.BlockDigests.Add(Fx.Block2);
    //         store.BlockDigests.Add(Fx.Block3);
    //         store.BlockDigests.Add(anotherBlock3);

    //         // store.AppendIndex(chainA, Fx.GenesisBlock.BlockHash);
    //         // store.AppendIndex(chainA, Fx.Block1.BlockHash);
    //         // store.AppendIndex(chainA, Fx.Block2.BlockHash);
    //         // store.AppendIndex(chainA, Fx.Block3.BlockHash);

    //         store.ForkBlockIndexes(chainA, chainB, Fx.Block2.BlockHash);
    //         // store.AppendIndex(chainB, anotherBlock3.BlockHash);

    //         Assert.Equal(
    //             [
    //                 Fx.Block2.BlockHash,
    //                 anotherBlock3.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainB).IterateHeights(2, 2));
    //         Assert.Equal(
    //             [
    //                 Fx.Block2.BlockHash,
    //                 anotherBlock3.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainB).IterateHeights(2));

    //         Assert.Equal(
    //             [
    //                 anotherBlock3.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainB).IterateHeights(3, 1));

    //         Assert.Equal(
    //             [
    //                 anotherBlock3.BlockHash,
    //             ],
    //             store.GetBlockHashes(chainB).IterateHeights(3));
    //     }

    //     [Fact]
    //     public void Copy()
    //     {
    //         using (StoreFixture fx = FxConstructor())
    //         using (StoreFixture fx2 = FxConstructor())
    //         {
    //             Libplanet.Store.Store s1 = fx.Store, s2 = fx2.Store;
    //             var preEval = ProposeGenesis(proposer: GenesisProposer.PublicKey);
    //             var genesis = preEval.Sign(
    //                 GenesisProposer,
    //                 default);
    //             var blocks = BlockChain.Create(genesis, fx.Options);

    //             // FIXME: Need to add more complex blocks/transactions.
    //             var key = new PrivateKey();
    //             var block = blocks.ProposeBlock(key);
    //             blocks.Append(block, CreateBlockCommit(block));
    //             block = blocks.ProposeBlock(key, CreateBlockCommit(blocks.Tip));
    //             blocks.Append(block, CreateBlockCommit(block));
    //             block = blocks.ProposeBlock(key, CreateBlockCommit(blocks.Tip));
    //             blocks.Append(block, CreateBlockCommit(block));

    //             s1.Copy(to: Fx.Store);
    //             store.Copy(to: s2);

    //             Assert.Equal(s1.Chains.Keys.ToHashSet(), [.. s2.Chains.Keys]);
    //             Assert.Equal(s1.ChainId, s2.ChainId);
    //             foreach (Guid chainId in s1.Chains.Keys)
    //             {
    //                 Assert.Equal(s1.GetBlockHashes(chainId).IterateHeights(), s2.GetBlockHashes(chainId).IterateHeights());
    //                 foreach (BlockHash blockHash in s1.GetBlockHashes(chainId).IterateHeights())
    //                 {
    //                     Assert.Equal(s1.Blocks[blockHash], s2.Blocks[blockHash]);
    //                 }
    //             }

    //             // ArgumentException is thrown if the destination store is not empty.
    //             Assert.Throws<ArgumentException>(() => store.Copy(fx2.Store));
    //         }
    //     }

    //     [Fact]
    //     public void GetBlock()
    //     {
    //         using (StoreFixture fx = FxConstructor())
    //         {
    //             Block genesisBlock = fx.GenesisBlock;
    //             Block block = ProposeNextBlock(
    //                 genesisBlock,
    //                 proposer: fx.Proposer);

    //             fx.Store.BlockDigests.Add(block);
    //             Block storedBlock = fx.Store.Blocks[block.BlockHash];

    //             Assert.Equal(block, storedBlock);
    //         }
    //     }

    //     [Fact]
    //     public void GetBlockCommit()
    //     {
    //         using (StoreFixture fx = FxConstructor())
    //         {
    //             // Commits with votes
    //             var height = 1;
    //             var round = 0;
    //             var hash = fx.Block2.BlockHash;
    //             var validators = Enumerable.Range(0, 4)
    //                 .Select(x => new PrivateKey())
    //                 .ToArray();
    //             var votes = validators.Select(validator => new VoteMetadata
    //             {
    //                 Height = height,
    //                 Round = round,
    //                 BlockHash = hash,
    //                 Timestamp = DateTimeOffset.UtcNow,
    //                 ValidatorPublicKey = validator.PublicKey,
    //                 ValidatorPower = BigInteger.One,
    //                 Flag = VoteFlag.PreCommit,
    //             }.Sign(validator)).ToImmutableArray();

    //             BlockCommit commit = new BlockCommit
    //             {
    //                 Height = height,
    //                 Round = round,
    //                 BlockHash = hash,
    //                 Votes = votes,
    //             };
    //             fx.Store.BlockCommits.Add(commit);
    //             BlockCommit storedCommitVotes =
    //                 fx.Store.BlockCommits[commit.BlockHash];

    //             Assert.Equal(commit, storedCommitVotes);
    //         }
    //     }

    //     [Fact]
    //     public void GetBlockCommitIndices()
    //     {
    //         using (StoreFixture fx = FxConstructor())
    //         {
    //             var votesOne = ImmutableArray<Vote>.Empty
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 1,
    //                     Round = 0,
    //                     BlockHash = fx.Block1.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = fx.Proposer.PublicKey,
    //                     ValidatorPower = fx.ProposerPower,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(fx.Proposer));
    //             var votesTwo = ImmutableArray<Vote>.Empty
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 2,
    //                     Round = 0,
    //                     BlockHash = fx.Block2.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = fx.Proposer.PublicKey,
    //                     ValidatorPower = fx.ProposerPower,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(fx.Proposer));

    //             BlockCommit[] blockCommits =
    //             [
    //                 new BlockCommit
    //                 {
    //                     Height = 1,
    //                     Round = 0,
    //                     BlockHash = fx.Block1.BlockHash,
    //                     Votes = votesOne,
    //                 },
    //                 new BlockCommit
    //                 {
    //                     Height = 2,
    //                     Round = 0,
    //                     BlockHash = fx.Block2.BlockHash,
    //                     Votes = votesTwo,
    //                 },
    //             ];

    //             foreach (var blockCommit in blockCommits)
    //             {
    //                 fx.Store.BlockCommits.Add(blockCommit);
    //             }

    //             IEnumerable<BlockHash> indices = fx.Store.BlockCommits.Keys;

    //             HashSet<long> indicesFromOperation = [.. indices.Select(hash => fx.Store.BlockCommits[hash].Height)];
    //             HashSet<long> expectedIndices = new HashSet<long>() { 1, 2 };

    //             Assert.Equal(indicesFromOperation, expectedIndices);
    //         }
    //     }

    //     [Fact]
    //     public void DeleteLastCommit()
    //     {
    //         using (StoreFixture fx = FxConstructor())
    //         {
    //             var validatorPrivateKey = new PrivateKey();
    //             var blockCommit = new BlockCommit
    //             {
    //                 BlockHash = Fx.GenesisBlock.BlockHash,
    //                 Votes =
    //                 [
    //                     new VoteMetadata
    //                     {
    //                         BlockHash = Fx.GenesisBlock.BlockHash,
    //                         Timestamp = DateTimeOffset.UtcNow,
    //                         ValidatorPublicKey = validatorPrivateKey.PublicKey,
    //                         ValidatorPower = BigInteger.One,
    //                         Flag = VoteFlag.PreCommit,
    //                     }.Sign(validatorPrivateKey)
    //                 ],
    //             };

    //             fx.Store.BlockCommits.Add(blockCommit);
    //             Assert.Equal(blockCommit, fx.Store.BlockCommits[blockCommit.BlockHash]);

    //             fx.Store.BlockCommits.Remove(blockCommit.BlockHash);
    //             Assert.Throws<KeyNotFoundException>(() => fx.Store.BlockCommits[blockCommit.BlockHash]);
    //         }
    //     }

    //     [Fact]
    //     public void IteratePendingEvidenceIds()
    //     {
    //         using (StoreFixture fx = FxConstructor())
    //         {
    //             var signer = TestUtils.ValidatorPrivateKeys[0];
    //             var duplicateVoteOne = ImmutableArray<Vote>.Empty
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 1,
    //                     Round = 0,
    //                     BlockHash = fx.Block1.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = signer.PublicKey,
    //                     ValidatorPower = BigInteger.One,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(signer))
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 1,
    //                     Round = 0,
    //                     BlockHash = fx.Block2.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = signer.PublicKey,
    //                     ValidatorPower = BigInteger.One,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(signer));
    //             var duplicateVoteTwo = ImmutableArray<Vote>.Empty
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 2,
    //                     Round = 0,
    //                     BlockHash = fx.Block2.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = signer.PublicKey,
    //                     ValidatorPower = BigInteger.One,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(signer))
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 2,
    //                     Round = 0,
    //                     BlockHash = fx.Block3.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = signer.PublicKey,
    //                     ValidatorPower = BigInteger.One,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(signer));

    //             EvidenceBase[] evidences =
    //             [
    //                 DuplicateVoteEvidence.Create(duplicateVoteOne[0], duplicateVoteOne[1], TestUtils.Validators),
    //                 DuplicateVoteEvidence.Create(duplicateVoteTwo[0], duplicateVoteTwo[1], TestUtils.Validators),
    //             ];

    //             foreach (var evidence in evidences)
    //             {
    //                 fx.Store.PendingEvidences.Add(evidence);
    //             }

    //             IEnumerable<EvidenceId> ids = fx.Store.PendingEvidences.Keys;
    //             Assert.Equal(evidences.Select(e => e.Id).ToHashSet(), [.. ids]);
    //         }
    //     }

    //     [Fact]
    //     public void ManipulatePendingEvidence()
    //     {
    //         using (StoreFixture fx = FxConstructor())
    //         {
    //             var signer = TestUtils.ValidatorPrivateKeys[0];
    //             var duplicateVote = ImmutableArray<Vote>.Empty
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 1,
    //                     Round = 0,
    //                     BlockHash = fx.Block1.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = signer.PublicKey,
    //                     ValidatorPower = BigInteger.One,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(signer))
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 1,
    //                     Round = 0,
    //                     BlockHash = fx.Block2.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = signer.PublicKey,
    //                     ValidatorPower = BigInteger.One,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(signer));
    //             var evidence = DuplicateVoteEvidence.Create(duplicateVote[0], duplicateVote[1], TestUtils.Validators);

    //             Assert.DoesNotContain(evidence.Id, fx.Store.PendingEvidences.Keys);

    //             fx.Store.PendingEvidences.Add(evidence);
    //             EvidenceBase storedEvidence = fx.Store.PendingEvidences[evidence.Id];

    //             Assert.Equal(evidence, storedEvidence);
    //             Assert.Contains(evidence.Id, fx.Store.PendingEvidences.Keys);

    //             fx.Store.PendingEvidences.Remove(evidence.Id);
    //             Assert.DoesNotContain(evidence.Id, fx.Store.PendingEvidences.Keys);
    //         }
    //     }

    //     [Fact]
    //     public void ManipulateCommittedEvidence()
    //     {
    //         using (StoreFixture fx = FxConstructor())
    //         {
    //             var signer = TestUtils.ValidatorPrivateKeys[0];
    //             var duplicateVote = ImmutableArray<Vote>.Empty
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 1,
    //                     Round = 0,
    //                     BlockHash = fx.Block1.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = signer.PublicKey,
    //                     ValidatorPower = BigInteger.One,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(signer))
    //                 .Add(new VoteMetadata
    //                 {
    //                     Height = 1,
    //                     Round = 0,
    //                     BlockHash = fx.Block2.BlockHash,
    //                     Timestamp = DateTimeOffset.UtcNow,
    //                     ValidatorPublicKey = signer.PublicKey,
    //                     ValidatorPower = BigInteger.One,
    //                     Flag = VoteFlag.PreCommit,
    //                 }.Sign(signer));
    //             var evidence = DuplicateVoteEvidence.Create(duplicateVote[0], duplicateVote[1], TestUtils.Validators);

    //             Assert.DoesNotContain(evidence.Id, fx.Store.CommittedEvidences.Keys);

    //             // fx.Store.PutCommittedEvidence(evidence);
    //             EvidenceBase storedEvidence = fx.Store.CommittedEvidences[evidence.Id];

    //             Assert.Equal(evidence, storedEvidence);
    //             Assert.Contains(evidence.Id, fx.Store.CommittedEvidences.Keys);

    //             // fx.Store.DeleteCommittedEvidence(evidence.Id);
    //             Assert.DoesNotContain(evidence.Id, fx.Store.CommittedEvidences.Keys);
    //         }
    //     }

    //     [Fact]
    //     public void ForkTxNonces()
    //     {
    //         Libplanet.Store.Store store = Fx.Store;
    //         Guid sourceChainId = Guid.NewGuid();
    //         Guid destinationChainId = Guid.NewGuid();
    //         store.GetNonceCollection(sourceChainId).Increase(Fx.Address1, 1);
    //         store.GetNonceCollection(sourceChainId).Increase(Fx.Address2, 2);
    //         store.GetNonceCollection(sourceChainId).Increase(Fx.Address3, 3);

    //         store.ForkTxNonces(sourceChainId, destinationChainId);

    //         Assert.Equal(1, store.GetNonceCollection(destinationChainId)[Fx.Address1]);
    //         Assert.Equal(2, store.GetNonceCollection(destinationChainId)[Fx.Address2]);
    //         Assert.Equal(3, store.GetNonceCollection(destinationChainId)[Fx.Address3]);

    //         store.GetNonceCollection(sourceChainId).Increase(Fx.Address1, 1);
    //         Assert.Equal(2, store.GetNonceCollection(sourceChainId)[Fx.Address1]);
    //         Assert.Equal(1, store.GetNonceCollection(destinationChainId)[Fx.Address1]);
    //     }

    //     [Fact]
    //     public void PruneOutdatedChains()
    //     {
    //         Libplanet.Store.Store store = Fx.Store;
    //         var chain = store.GetChain(store.ChainId);
    //         chain.Blocks.Add(Fx.GenesisBlock);
    //         chain.Blocks.Add(Fx.Block1);
    //         chain.Blocks.Add(Fx.Block2);
    //         chain.Blocks.Add(Fx.Block3);

    //         Guid cid1 = Guid.NewGuid();
    //         // store.AppendIndex(cid1, Fx.GenesisBlock.BlockHash);
    //         // store.AppendIndex(cid1, Fx.Block1.BlockHash);
    //         // store.AppendIndex(cid1, Fx.Block2.BlockHash);
    //         Assert.Single(store.Chains.Keys);
    //         Assert.Equal(
    //             [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
    //             store.GetBlockHashes(cid1).IterateHeights(0, null));

    //         Guid cid2 = Guid.NewGuid();
    //         store.ForkBlockIndexes(cid1, cid2, Fx.Block1.BlockHash);
    //         // store.AppendIndex(cid2, Fx.Block2.BlockHash);
    //         // store.AppendIndex(cid2, Fx.Block3.BlockHash);
    //         Assert.Equal(2, store.Chains.Keys.Count);
    //         Assert.Equal(
    //             [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash, Fx.Block3.BlockHash],
    //             store.GetBlockHashes(cid2).IterateHeights(0, null));

    //         Guid cid3 = Guid.NewGuid();
    //         store.ForkBlockIndexes(cid1, cid3, Fx.Block2.BlockHash);
    //         Assert.Equal(3, store.Chains.Keys.Count);
    //         Assert.Equal(
    //             [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
    //             store.GetBlockHashes(cid3).IterateHeights(0, null));

    //         Assert.Throws<InvalidOperationException>(() => store.PruneOutdatedChains());
    //         store.PruneOutdatedChains(true);
    //         store.ChainId = cid3;
    //         store.PruneOutdatedChains();
    //         Assert.Single(store.Chains.Keys);
    //         Assert.Equal(
    //             [Fx.GenesisBlock.BlockHash, Fx.Block1.BlockHash, Fx.Block2.BlockHash],
    //             store.GetBlockHashes(cid3).IterateHeights(0, null));
    //         Assert.Equal(3, store.Chains[cid3].Height);
    //     }

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

        public override int GetHashCode() => ModelUtility.GetHashCode(this);

        public bool Equals(AtomicityTestAction? other) => ModelUtility.Equals(this, other);

        protected override void OnExecute(IWorldContext world, IActionContext context)
        {
        }
    }
}
