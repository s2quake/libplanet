namespace Libplanet.Store;

public interface IDatabase : IReadOnlyDictionary<string, IKeyValueStore>
{
    IKeyValueStore GetOrAdd(string key);

    public bool Remove(string key);
}
