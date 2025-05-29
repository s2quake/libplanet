namespace Libplanet.Data;

public interface IDatabase : IReadOnlyDictionary<string, ITable>
{
    ITable GetOrAdd(string name);

    bool TryRemove(string name);
}
