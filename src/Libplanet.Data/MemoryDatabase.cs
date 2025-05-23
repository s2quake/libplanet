namespace Libplanet.Data;

public sealed class MemoryDatabase : Database<MemoryTable>
{
    protected override MemoryTable Create(string key) => [];
}
