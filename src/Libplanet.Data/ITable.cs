namespace Libplanet.Data;

public interface ITable : IDictionary<string, byte[]>
{
    event EventHandler? Cleared;

    string Name { get; }
}
