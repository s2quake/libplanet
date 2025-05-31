namespace Libplanet.Data.Tests;

public sealed class MemoryTableTest : TableTestBase<MemoryTable>
{
    public override MemoryTable CreateTable(string name) => new(name);

    [Fact]
    public void MemoryTable()
    {
        var table = new MemoryTable();
        Assert.Equal(string.Empty, table.Name);
    }
}
