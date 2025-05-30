namespace Libplanet.Data.Tests;

public sealed class MemoryTableTest : TableTestBase<MemoryTable>
{
    public override MemoryTable CreateTable(string name) => new(name);
}
