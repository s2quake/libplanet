using Libplanet.Data;

namespace Libplanet.Tests.Store.Structures;

public class DefaultKeyValueStoreTest : TableTestBase, IDisposable
{
    private readonly DefaultTable _defaultKeyValueStore;

    public DefaultKeyValueStoreTest()
    {
        // Memory mode.
        _defaultKeyValueStore = new DefaultTable();
        InitializePreStoredData();
    }

    protected override IDictionary<string, byte[]> KeyValueStore => _defaultKeyValueStore;

    public void Dispose()
    {
        _defaultKeyValueStore.Dispose();
    }
}
