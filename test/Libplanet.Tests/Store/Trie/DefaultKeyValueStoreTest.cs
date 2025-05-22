using Libplanet.Store;

namespace Libplanet.Tests.Store.Trie;

public class DefaultKeyValueStoreTest : KeyValueStoreTest, IDisposable
{
    private readonly DefaultTable _defaultKeyValueStore;

    public DefaultKeyValueStoreTest()
    {
        // Memory mode.
        KeyValueStore = _defaultKeyValueStore = new DefaultTable(null);
        InitializePreStoredData();
    }

    public void Dispose()
    {
        _defaultKeyValueStore.Dispose();
    }
}
