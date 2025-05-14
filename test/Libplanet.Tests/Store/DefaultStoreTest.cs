using System.IO;
using Libplanet.Store;
using Xunit.Abstractions;

namespace Libplanet.Tests.Store;

public sealed class DefaultStoreTest : StoreTest, IDisposable
{
    private readonly DefaultStoreFixture _fx;

    public DefaultStoreTest(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;
        Fx = _fx = new DefaultStoreFixture(memory: false);
        FxConstructor = () => new DefaultStoreFixture(memory: false);
    }

    protected override ITestOutputHelper TestOutputHelper { get; }

    protected override StoreFixture Fx { get; }

    protected override Func<StoreFixture> FxConstructor { get; }

    public void Dispose()
    {
        _fx?.Dispose();
    }

    [Fact]
    public void ConstructorAcceptsRelativePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"defaultstore_{Guid.NewGuid()}");
        var store = new Libplanet.Store.Store(new DefaultDatabase(path));

        store.PendingTransactions.Add(Fx.Transaction1);

        // If CWD is changed after DefaultStore instance was created
        // the instance should work as it had been.
        store.PendingTransactions.Add(Fx.Transaction2);

        // The following `identicalStore' instance should be identical to
        // the `store' instance above, i.e., views the same data.
        var identicalStore = new Libplanet.Store.Store(new DefaultDatabase(path));
        Assert.Equal(Fx.Transaction1, identicalStore.PendingTransactions[Fx.Transaction1.Id]);
        Assert.Equal(Fx.Transaction2, identicalStore.PendingTransactions[Fx.Transaction2.Id]);
    }

    // [Fact]
    // public void Loader()
    // {
    //     // TODO: Test query parameters as well.
    //     string tempDirPath = Path.GetTempFileName();
    //     File.Delete(tempDirPath);
    //     var uri = new Uri(tempDirPath, UriKind.Absolute);
    //     uri = new Uri("default+" + uri);
    //     (Libplanet.Store.Store Store, TrieStateStore StateStore)? pair = StoreLoaderAttribute.LoadStore(uri);
    //     Assert.NotNull(pair);
    //     Libplanet.Store.Store store = pair.Value.Store;
    //     Assert.IsAssignableFrom<DefaultStore>(store);
    //     var stateStore = (TrieStateStore)pair.Value.StateStore;
    //     Assert.IsAssignableFrom<DefaultTable>(stateStore.StateKeyValueStore);
    // }
}
