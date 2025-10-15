using System.IO;
using Libplanet.Commands;
using Libplanet.Tests.Store;

namespace Libplanet.Commands.Tests.Commands;

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
            _storeFixtures = [new MemoryRepositoryFixture()];
        }
        catch (TypeInitializationException)
        {
            throw new InvalidOperationException("RocksDB is not available.");
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
            if (storeFixture.Repository is IDisposable disposableRepository)
            {
                disposableRepository.Dispose();
            }
        }

        Console.SetOut(_originalWriter);
        _originalWriter.Dispose();
        GC.SuppressFinalize(this);
    }
}
