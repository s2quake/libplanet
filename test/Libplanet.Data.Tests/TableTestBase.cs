#pragma warning disable S4143 // Collection elements should not be replaced unconditionally
using System.Collections;

namespace Libplanet.Data.Tests;

public abstract class TableTestBase
{
    public abstract ITable CreateTable(string key);

    [Fact]
    public void Get_After_Add()
    {
        var table = CreateTable(nameof(Get_After_Add));
        var key = "testKey";
        var expectedValue = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, expectedValue);
        var actualValue = table[key];
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void Get_After_Set()
    {
        var table = CreateTable(nameof(Get_After_Add));
        var key = "testKey";
        var expectedValue = new byte[] { 1, 2, 3, 4, 5 };
        table[key] = expectedValue;
        var actualValue = table[key];
        Assert.Equal(expectedValue, actualValue);
    }

    [Fact]
    public void Get_NonExistentKey_Throw()
    {
        var table = CreateTable(nameof(Get_NonExistentKey_Throw));
        var key = "nonExistentKey";
        Assert.Throws<KeyNotFoundException>(() => _ = table[key]);
    }

    [Fact]
    public void Set()
    {
        Dictionary<string, string> d;
        var table = CreateTable(nameof(Set));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table[key] = value;
        Assert.Equal(value, table[key]);
    }

    [Fact]
    public void Set_ExistingKey_Overwrite()
    {
        var table = CreateTable(nameof(Set_ExistingKey_Overwrite));
        var key = "testKey";
        var initialValue = new byte[] { 1, 2, 3, 4, 5 };
        table[key] = initialValue;
        var newValue = new byte[] { 6, 7, 8, 9, 10 };
        table[key] = newValue;
        Assert.Equal(newValue, table[key]);
    }

    [Fact]
    public void Set_After_Add()
    {
        var table = CreateTable(nameof(Set_After_Add));
        var key = "testKey";
        var initialValue = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, initialValue);
        var newValue = new byte[] { 6, 7, 8, 9, 10 };
        table[key] = newValue;
        Assert.Equal(newValue, table[key]);
    }

    [Fact]
    public void Add()
    {
        var table = CreateTable(nameof(Add));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        Assert.Single(table);
    }

    [Fact]
    public void Add_ExistingKey_Throw()
    {
        var table = CreateTable(nameof(Add_ExistingKey_Throw));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        Assert.Throws<ArgumentException>(() => table.Add(key, value));
    }

    [Fact]
    public void Add_EmptyKey()
    {
        var table = CreateTable(nameof(Add_EmptyKey));
        var key = string.Empty;
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        Assert.Single(table);
        Assert.Equal(value, table[key]);
    }

    [Fact]
    public void Add_EmptyValue()
    {
        var table = CreateTable(nameof(Add_EmptyValue));
        var key = "testKey";
        var value = Array.Empty<byte>();
        table.Add(key, value);
        Assert.Single(table);
        Assert.Equal(value, table[key]);
    }

    [Fact]
    public void ContainsKey()
    {
        var table = CreateTable(nameof(ContainsKey));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        Assert.True(table.ContainsKey(key));
        Assert.False(table.ContainsKey("nonExistentKey"));
    }

    [Fact]
    public void Remove()
    {
        var table = CreateTable(nameof(Remove));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        Assert.True(table.Remove(key));
        Assert.False(table.ContainsKey(key));
    }

    [Fact]
    public void Remove_NonExistentKey_ReturnsFalse()
    {
        var table = CreateTable(nameof(Remove_NonExistentKey_ReturnsFalse));
        var key = "nonExistentKey";
        Assert.False(table.Remove(key));
    }

    [Fact]
    public void TryGetValue()
    {
        var table = CreateTable(nameof(TryGetValue));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        Assert.True(table.TryGetValue(key, out var actualValue));
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public void TryGetValue_NonExistentKey_ReturnsFalse()
    {
        var table = CreateTable(nameof(TryGetValue_NonExistentKey_ReturnsFalse));
        var key = "nonExistentKey";
        Assert.False(table.TryGetValue(key, out _));
    }

    [Fact]
    public void Count()
    {
        var table = CreateTable(nameof(Count));
        Assert.Empty(table);
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        table.Add(key1, value1);
        Assert.Single(table);
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        table.Add(key2, value2);
        Assert.Equal(2, table.Count);
        table.Remove(key1);
        Assert.Single(table);
        table.Clear();
        Assert.Empty(table);
    }

    [Fact]
    public void IsReadOnly()
    {
        var table = CreateTable(nameof(IsReadOnly));
        Assert.False(table.IsReadOnly);
    }

    [Fact]
    public void CopyTo()
    {
        var table = CreateTable(nameof(CopyTo));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        table.Add(key1, value1);
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        table.Add(key2, value2);

        var array = new KeyValuePair<string, byte[]>[2];
        table.CopyTo(array, 0);
        Assert.Equal(2, array.Length);
        Assert.Contains(new KeyValuePair<string, byte[]>(key1, value1), array);
        Assert.Contains(new KeyValuePair<string, byte[]>(key2, value2), array);
    }

    [Fact]
    public void Clear()
    {
        var table = CreateTable(nameof(Clear));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        table.Add(key1, value1);
        Assert.Single(table);
        table.Clear();
        Assert.Empty(table);
        Assert.False(table.ContainsKey(key1));
        Assert.Throws<KeyNotFoundException>(() => _ = table[key1]);
    }

    [Fact]
    public void EnumerableGeneric()
    {
        var table = CreateTable(nameof(EnumerableGeneric));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        table.Add(key1, value1);
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        table.Add(key2, value2);

        var enumerator = table.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<string, byte[]>(key1, value1), enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<string, byte[]>(key2, value2), enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void ImplicitEnumerable()
    {
        var table = CreateTable(nameof(ImplicitEnumerable));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        table.Add(key1, value1);
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        table.Add(key2, value2);

        var enumerator = ((IEnumerable)table).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<string, byte[]>(key1, value1), enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<string, byte[]>(key2, value2), enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }
}
