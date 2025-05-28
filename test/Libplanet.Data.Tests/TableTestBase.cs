namespace Libplanet.Data.Tests;

public abstract class TableTestBase
{
    public abstract ITable CreateTable(string key);

    [Fact]
    public void Add()
    {
        var table = CreateTable(nameof(Add));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        Assert.Single(table);
    }
}
