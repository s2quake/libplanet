namespace Libplanet.Store;

public interface IDatabase : IReadOnlyDictionary<string, IKeyValueStore>, IDisposable
{
    IKeyValueStore GetOrAdd(string key);

    public bool Remove(string key);
}
