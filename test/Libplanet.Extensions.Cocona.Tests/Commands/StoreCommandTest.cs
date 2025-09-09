using System.IO;
using global::Cocona;
using Libplanet.State;
using Libplanet.Extensions.Cocona.Commands;
using Libplanet.Tests;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Libplanet.TestUtilities;

namespace Libplanet.Extensions.Cocona.Tests.Commands;

[Trait("CircleCI", "Skip")]
public sealed class StoreCommandTest : IDisposable
{
    private readonly ImmutableArray<RepositoryFixture> _storeFixtures;
    private readonly ITestOutputHelper _testOutput;
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly Block _genesisBlock;
    private readonly Block _block1;
    private readonly Block _block2;
    private readonly Block _block3;
    private readonly Block _block4;
    private readonly Block _block5;
    private readonly Transaction _transaction1;
    private readonly Transaction _transaction2;
    private readonly Transaction _transaction3;
    private readonly Transaction _transaction4;

    public StoreCommandTest(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _originalOut = Console.Out;
        _originalError = Console.Error;

        try
        {
            _storeFixtures = ImmutableArray.Create<RepositoryFixture>(
                new MemoryRepositoryFixture());
        }
        catch (TypeInitializationException)
        {
            throw new InvalidOperationException("RocksDB is not available.");
        }

        _genesisBlock =
                TestUtils.ProposeGenesisBlock(TestUtils.GenesisProposer);
        _transaction1 = DummyTransaction();
        _transaction2 = DummyTransaction();
        _transaction3 = DummyTransaction();
        _transaction4 = DummyTransaction();

        _block1 = TestUtils.ProposeNextBlock(
                _genesisBlock,
                TestUtils.GenesisProposer,
                [_transaction1],
                lastCommit: null);
        _block2 = TestUtils.ProposeNextBlock(
                _block1,
                TestUtils.GenesisProposer,
                [_transaction2],
                lastCommit: TestUtils.CreateBlockCommit(_block1));
        _block3 = TestUtils.ProposeNextBlock(
                _block2,
                TestUtils.GenesisProposer,
                [_transaction3],
                lastCommit: TestUtils.CreateBlockCommit(_block2));
        _block4 = TestUtils.ProposeNextBlock(
                _block3,
                TestUtils.GenesisProposer,
                [_transaction3],
                lastCommit: TestUtils.CreateBlockCommit(_block3));
        _block5 = TestUtils.ProposeNextBlock(
                _block4,
                TestUtils.GenesisProposer,
                lastCommit: TestUtils.CreateBlockCommit(_block4));

        var guid = Guid.NewGuid();
        foreach (var storeFixture in _storeFixtures)
        {
            var store = storeFixture.Repository;
            var chain = store;
            store.BlockDigests.Add(_genesisBlock);
            chain.BlockHashes.Add(_genesisBlock);

            store.BlockDigests.Add(_block1);
            chain.BlockHashes.Add(_block1);
            store.PendingTransactions.Add(_transaction1);

            store.BlockDigests.Add(_block2);
            chain.BlockHashes.Add(_block2);
            store.PendingTransactions.Add(_transaction2);

            store.BlockDigests.Add(_block3);
            chain.BlockHashes.Add(_block3);
            store.PendingTransactions.Add(_transaction3);

            store.BlockDigests.Add(_block4);
            chain.BlockHashes.Add(_block4);

            if (storeFixture.Repository is IDisposable disposableRepository)
            {
                disposableRepository.Dispose();
            }
        }
    }

    [Fact]
    public void TestInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() =>
            new StoreCommand().BlockByHash(
                "rocksdb+file+file://" + "/blah",
                "dummy"));
        Assert.Throws<ArgumentException>(() =>
            new StoreCommand().BlockByHash(
                "rocksdb+memory://" + "/blah",
                "dummy"));
        Assert.Throws<ArgumentException>(() =>
            new StoreCommand().BlockByHash(
                "leveldb://" + "/blah",
                "dummy"));
    }

    [Fact]
    public void TestBlockByTxIdNotExist()
    {
        foreach (var fx in _storeFixtures)
        {
            new StoreCommand().BuildIndexTxBlock(
                fx.Scheme + fx.Path,
                0,
                10);
        }

        foreach (var fx in _storeFixtures)
        {
            Assert.Throws<CommandExitedException>(() =>
                new StoreCommand().BlocksByTxId(
                    fx.Scheme + fx.Path,
                    _transaction4.Id.ToString()));
        }
    }

    [Fact]
    public void TestBlockByTxIdTwo()
    {
        foreach (var fx in _storeFixtures)
        {
            new StoreCommand().BuildIndexTxBlock(
                fx.Scheme + fx.Path,
                0,
                10);
        }

        foreach (var fx in _storeFixtures)
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            Console.SetOut(stdout);
            Console.SetError(stderr);
            new StoreCommand().BlocksByTxId(
                fx.Scheme + fx.Path,
                _transaction3.Id.ToString());
            var actual = stdout.ToString();
            _testOutput.WriteLine($"stdout:\n  {actual.Replace("\n", "\n  ")}\n");
            _testOutput.WriteLine($"stderr:\n  {stderr.ToString().Replace("\n", "\n  ")}\n");
            var expected = Utils.SerializeHumanReadable(new[] { _block3, _block4 });
            if (expected.TrimEnd() != actual.TrimEnd())
            {
                expected = Utils.SerializeHumanReadable(new[] { _block4, _block3 });
            }

            _testOutput.WriteLine($"expected:\n  {expected.Replace("\n", "\n  ")}\n");
            Assert.Equal(
                expected.TrimEnd(),
                actual.TrimEnd(),
                ignoreLineEndingDifferences: true,
                ignoreWhiteSpaceDifferences: true);
        }
    }

    [Fact]
    public void TestBlockHashesByTxId()
    {
        foreach (var fx in _storeFixtures)
        {
            new StoreCommand().BuildIndexTxBlock(
                fx.Scheme + fx.Path,
                0,
                10);
        }

        foreach (var fx in _storeFixtures)
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            new StoreCommand().BlockHashesByTxId(
                fx.Scheme + fx.Path,
                _transaction3.Id.ToString());
            var actual = sw.ToString();
            var expected = Utils.SerializeHumanReadable(new[] { _block3.BlockHash, _block4.BlockHash });
            if (expected.TrimEnd() != actual.TrimEnd())
            {
                expected = Utils.SerializeHumanReadable(new[] { _block4.BlockHash, _block3.BlockHash });
            }

            Assert.Equal(expected.TrimEnd(), actual.TrimEnd());
        }
    }

    [Fact]
    public void TestBuildIndexTxBlockBlockByTxId()
    {
        foreach (var fx in _storeFixtures)
        {
            new StoreCommand().BuildIndexTxBlock(
                fx.Scheme + fx.Path,
                0,
                10);
        }

        foreach (var fx in _storeFixtures)
        {
            void AssertTxBlockIndex(
                Transaction tx,
                Block block)
            {
                using var sw = new StringWriter();
                Console.SetOut(sw);
                new StoreCommand().BlocksByTxId(
                    fx.Scheme + fx.Path,
                    tx.Id.ToString());
                var actual = sw.ToString();
                var expected = Utils.SerializeHumanReadable(new[] { block });
                Assert.Equal(expected.TrimEnd(), actual.TrimEnd());
            }

            AssertTxBlockIndex(_transaction1, _block1);
            AssertTxBlockIndex(_transaction2, _block2);
        }
    }

    [Fact]
    public void TestBlockByHashNotExists()
    {
        foreach (var fx in _storeFixtures)
        {
            Assert.Throws<CommandExitedException>(() =>
                new StoreCommand().BlockByHash(
                    fx.Scheme + fx.Path,
                    _block5.BlockHash.ToString()));
        }
    }

    [Fact]
    public void TestBlockByHash()
    {
        foreach (var fx in _storeFixtures)
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            new StoreCommand().BlockByHash(
                fx.Scheme + fx.Path,
                _block1.BlockHash.ToString());
            var actual = sw.ToString();
            var expected = Utils.SerializeHumanReadable(_block1);
            Assert.Equal(expected.TrimEnd(), actual.TrimEnd());
        }
    }

    [Fact]
    public void TestBlockByIndexNotExists()
    {
        foreach (var fx in _storeFixtures)
        {
            Assert.Throws<CommandExitedException>(() =>
                new StoreCommand().BlockByIndex(
                    fx.Scheme + fx.Path,
                    9999999 * 100));
        }
    }

    [Fact]
    public void TestBlockByIndex()
    {
        foreach (var fx in _storeFixtures)
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            new StoreCommand().BlockByIndex(fx.Scheme + fx.Path, 0);
            var actual = sw.ToString();
            var expected = Utils.SerializeHumanReadable(_genesisBlock);
            Assert.Equal(expected.TrimEnd(), actual.TrimEnd());
        }
    }

    [Fact]
    public void TestTxByIdNotExists()
    {
        foreach (var fx in _storeFixtures)
        {
            Assert.Throws<CommandExitedException>(() =>
                new StoreCommand().TxById(
                    fx.Scheme + fx.Path,
                    fx.Transaction2.Id.ToString()));
        }
    }

    [Fact]
    public void TestTxById()
    {
        foreach (var fx in _storeFixtures)
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);
            new StoreCommand().TxById(
                fx.Scheme + fx.Path,
                _transaction1.Id.ToString());
            var actual = sw.ToString();
            var expected = Utils.SerializeHumanReadable(_transaction1);
            Assert.Equal(expected.TrimEnd(), actual.TrimEnd());
        }
    }

    public void Dispose()
    {
        foreach (var storeFixture in _storeFixtures)
        {
            if (storeFixture.Repository is IDisposable disposableRepository)
            {
                disposableRepository.Dispose();
            }
        }

        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        _originalOut.Dispose();
        _originalError.Dispose();
        GC.SuppressFinalize(this);
    }

    private Transaction DummyTransaction()
    {
        var signer = Rand.Signer();
        return new TransactionMetadata
        {
            Nonce = 0,
            Signer = signer.Address,
            GenesisBlockHash = _genesisBlock.BlockHash,
            Actions = new[] { new Utils.DummyAction() }.ToBytecodes(),
        }.Sign(signer);
    }
}
