namespace Libplanet.Data;

internal interface IRepositoryIndex
{
    string Name { get; }

    ITable Table { get; }

    void Clear();
}
