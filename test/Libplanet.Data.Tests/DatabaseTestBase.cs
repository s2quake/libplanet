using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Libplanet.TestUtilities;
using Libplanet.Types.Tests;

namespace Libplanet.Data.Tests;

public abstract class DatabaseTestBase<TDatabase>
    where TDatabase : IDatabase
{
    public abstract TDatabase CreateDatabase(string name);

    [Fact]
    public void GetOrAdd()
    {
        var database = CreateDatabase(nameof(GetOrAdd));
        Assert.Empty(database);
        var table = database.GetOrAdd("test");
        Assert.Single(database);
        Assert.Contains("test", database.Keys);
        Assert.Contains(table, database.Values);
    }

    [Fact]
    public void ContainsKey()
    {
        var database = CreateDatabase(nameof(ContainsKey));
        Assert.False(database.ContainsKey("test"));
        var table = database.GetOrAdd("test");
        Assert.True(database.ContainsKey("test"));
        Assert.Contains(table, database.Values);
    }

    [Fact]
    public void TryGetValue()
    {
        var database = CreateDatabase(nameof(TryGetValue));
        Assert.False(database.TryGetValue("test", out _));
        var table = database.GetOrAdd("test");
        Assert.True(database.TryGetValue("test", out var value));
        Assert.Equal(table, value);
    }

    [Fact]
    public void TryRemove()
    {
        var database = CreateDatabase(nameof(TryRemove));
        var table = database.GetOrAdd("test");
        Assert.True(database.ContainsKey("test"));
        Assert.True(database.TryRemove("test"));
        Assert.False(database.TryRemove("nonexistent"));
        Assert.False(database.ContainsKey("test"));
        Assert.DoesNotContain(table, database.Values);
    }

    [Fact]
    public void Count()
    {
        var database = CreateDatabase(nameof(Count));
        Assert.Empty(database);
        database.GetOrAdd("test1");
        database.GetOrAdd("test2");
        Assert.Equal(2, database.Count);
    }

    [Fact]
    public void Keys()
    {
        var database = CreateDatabase(nameof(Keys));
        Assert.Empty(database.Keys);
        database.GetOrAdd("test1");
        database.GetOrAdd("test2");
        Assert.Contains("test1", database.Keys);
        Assert.Contains("test2", database.Keys);
    }

    [Fact]
    public void Values()
    {
        var database = CreateDatabase(nameof(Values));
        Assert.Empty(database.Values);
        var table1 = database.GetOrAdd("test1");
        var table2 = database.GetOrAdd("test2");
        Assert.Contains(table1, database.Values);
        Assert.Contains(table2, database.Values);
        database.TryRemove("test1");
        Assert.DoesNotContain(table1, database.Values);
    }

    [Fact]
    public void IReadOnlyDictionary_Values()
    {
        var database = CreateDatabase(nameof(IReadOnlyDictionary_Values));
        var dictionary = (IReadOnlyDictionary<string, ITable>)database;
        Assert.Empty(dictionary.Values);
        var table1 = database.GetOrAdd("test1");
        var table2 = database.GetOrAdd("test2");
        Assert.Contains(table1, dictionary.Values);
        Assert.Contains(table2, dictionary.Values);
    }

    [Fact]
    public void IReadOnlyDictionary_Get()
    {
        var database = CreateDatabase(nameof(IReadOnlyDictionary_Get));
        var dictionary = (IReadOnlyDictionary<string, ITable>)database;
        Assert.Throws<KeyNotFoundException>(() => dictionary["test"]);
        var table = database.GetOrAdd("test");
        Assert.Equal(table, dictionary["test"]);
    }

    [Fact]
    public void Get()
    {
        var database = CreateDatabase(nameof(Get));
        Assert.Throws<KeyNotFoundException>(() => database["test"]);
        var table = database.GetOrAdd("test");
        Assert.Equal(table, database["test"]);
    }

    [Fact]
    public void GetEnumerator()
    {
        var database = CreateDatabase(nameof(GetEnumerator));
        var enumerator = database.GetEnumerator();
        Assert.False(enumerator.MoveNext());
        database.GetOrAdd("test1");
        database.GetOrAdd("test2");
        var keys = new List<string>();
        foreach (var kvp in database)
        {
            keys.Add(kvp.Key);
        }
        Assert.Contains("test1", keys);
        Assert.Contains("test2", keys);
    }

    [Fact]
    public void IEnumerable_GetEnumerator()
    {
        var database = CreateDatabase(nameof(IEnumerable_GetEnumerator));
        var enumerator = ((IEnumerable)database).GetEnumerator();
        Assert.False(enumerator.MoveNext());
        var table1 = database.GetOrAdd("test1");
        var table2 = database.GetOrAdd("test2");

        var keyList = new List<string>();
        var valueList = new List<ITable>();
        foreach (var kvp in (IEnumerable)database)
        {
            var key = kvp.GetType().GetProperty("Key")!.GetValue(kvp);
            var value = kvp.GetType().GetProperty("Value")!.GetValue(kvp);
            keyList.Add((string)key!);
            valueList.Add((ITable)value!);
        }

        Assert.Contains("test1", keyList);
        Assert.Contains("test2", keyList);
        Assert.Contains(table1, valueList);
        Assert.Contains(table2, valueList);
    }

    [Fact]
    public void IDatabase_GetOrAdd()
    {
        var database = (IDatabase)CreateDatabase(nameof(IDatabase_GetOrAdd));
        Assert.Empty(database);
        var table = database.GetOrAdd("test");
        Assert.Single(database);
        Assert.Contains("test", database.Keys);
        Assert.Contains(table, database.Values);
    }

    [Fact]
    public void GetOrAdd_Parallels()
    {
        var database = CreateDatabase(nameof(GetOrAdd_Parallels));
        var keys = RandomUtility.Array(RandomUtility.Word, 20);
        var tables = new ConcurrentBag<ITable>();

        Parallel.ForEach(keys, (key) =>
        {
            tables.Add(database.GetOrAdd(key));
        });

        Assert.Equal(keys.Length, tables.Count);
    }
}
