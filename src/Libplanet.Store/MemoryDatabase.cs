namespace Libplanet.Store;

public sealed class MemoryDatabase : Database<MemoryKeyValueStore>
{
    protected override MemoryKeyValueStore Create(string key) => [];
}
