using System.IO;
using Libplanet.Extensions.Cocona.Commands;
using Libplanet.Data.RocksDB.Tests;
using Libplanet.Tests.Store;

namespace Libplanet.Extensions.Cocona.Tests.Commands;

public sealed class StatsCommandTest : IDisposable
{
    private readonly ImmutableArray<RepositoryFixture> _storeFixtures;
    private readonly StatsCommand _command;
    private readonly TextWriter _originalWriter;

    public StatsCommandTest()
    {
        _command = new StatsCommand();
        _originalWriter = Console.Out;
        try
        {
            _storeFixtures = [new MemoryRepositoryFixture(), new RocksDBStoreFixture()];
        }
        catch (TypeInitializationException)
        {
            throw new SkipException("RocksDB is not available.");
        }

        foreach (var storeFixture in _storeFixtures)
        {
            var store = storeFixture.Repository;
            var chain = store;
            store.BlockDigests.Add(storeFixture.Block1);
            chain.BlockHashes.Add(storeFixture.Block1);
            store.PendingTransactions.Add(storeFixture.Transaction1);
        }
    }

    [Fact]
    [Trait("CircleCI", "Skip")]
    public void SummaryInvalidArguments()
    {
        string badPathFormat = "rocksdb+foo+bar://" + "/bar";
        string badPathScheme = "foo://" + "/bar";
        int badOffset = int.MaxValue;
        int badLimit = 0;

        foreach (var storeFixture in _storeFixtures)
        {
            _command.Summary(
                repository: storeFixture.Repository,
                header: true,
                offset: 0,
                limit: 1);

            Assert.Throws<ArgumentException>(() =>
                _command.Summary(
                    path: badPathFormat,
                    header: true,
                    offset: 0,
                    limit: 1));
            Assert.Throws<ArgumentException>(() =>
                _command.Summary(
                    path: badPathScheme,
                    header: true,
                    offset: 0,
                    limit: 1));
            Assert.Throws<ArgumentException>(() =>
                _command.Summary(
                    repository: storeFixture.Repository,
                    header: true,
                    offset: badOffset,
                    limit: 1));
            Assert.Throws<ArgumentException>(() =>
                _command.Summary(
                    repository: storeFixture.Repository,
                    header: true,
                    offset: 0,
                    limit: badLimit));
        }
    }

    public void Dispose()
    {
        foreach (var storeFixture in _storeFixtures)
        {
            storeFixture.Repository.Dispose();
        }

        Console.SetOut(_originalWriter);
        _originalWriter.Dispose();
        GC.SuppressFinalize(this);
    }
}
