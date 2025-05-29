namespace Libplanet.Data;

public interface IDatabase : IReadOnlyDictionary<string, ITable>
{
    ITable GetOrAdd(string key);

    bool TryRemove(string key);
}
