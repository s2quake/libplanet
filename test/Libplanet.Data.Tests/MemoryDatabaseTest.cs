namespace Libplanet.Data.Tests;

public sealed class MemoryDatabaseTest : DatabaseTestBase<MemoryDatabase>
{
    public override MemoryDatabase CreateDatabase(string name) => [];

    [Fact]
    public void Database_Values()
    {
        var database = CreateDatabase(nameof(Database_Values));
        Assert.Empty(database.Values);

        var table1 = database.GetOrAdd("table1");
        var table2 = database.GetOrAdd("table2");

        Assert.Contains(table1, database.Values);
        Assert.Contains(table2, database.Values);

        database.TryRemove("table1");
        Assert.Single(database.Values);
        Assert.DoesNotContain(table1, database.Values);
        Assert.Contains(table2, database.Values);
    }

    [Fact]
    public void Database_Get()
    {
        var database = CreateDatabase(nameof(Database_Get));
        Assert.Empty(database);

        var table1 = database.GetOrAdd("table1");
        var table2 = database.GetOrAdd("table2");

        Assert.Equal(table1, database["table1"]);
        Assert.Equal(table2, database["table2"]);

        Assert.Throws<KeyNotFoundException>(() => _ = database["nonexistent_table"]);
    }

    [Fact]
    public void Database_TryGetValue()
    {
        var database = CreateDatabase(nameof(Database_TryGetValue));
        Assert.Empty(database);

        var table1 = database.GetOrAdd("table1");
        var table2 = database.GetOrAdd("table2");

        Assert.True(database.TryGetValue("table1", out var value1));
        Assert.Equal(table1, value1);

        Assert.True(database.TryGetValue("table2", out var value2));
        Assert.Equal(table2, value2);

        Assert.False(database.TryGetValue("nonexistent_table", out _));
    }
}
