namespace Libplanet.Store;

public interface IDatabase : IReadOnlyDictionary<string, ITable>, IDisposable
{
    ITable GetOrAdd(string key);

    public bool Remove(string key);
}
