#pragma warning disable S4143 // Collection elements should not be replaced unconditionally
using System.Collections;

namespace Libplanet.Data.Tests;

public abstract class TableTestBase<TTable>
    where TTable : ITable
{
    public abstract TTable CreateTable(string name);

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
    public void IEnumerableGeneric_GetEnumerator()
    {
        var table = CreateTable(nameof(IEnumerableGeneric_GetEnumerator));
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
    public void IEnumerable_GetEnumerator()
    {
        var table = CreateTable(nameof(IEnumerable_GetEnumerator));
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

    [Fact]
    public void ICollection_Add()
    {
        var table = CreateTable(nameof(ICollection_Add));
        var collection = (ICollection<KeyValuePair<string, byte[]>>)table;
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        collection.Add(new KeyValuePair<string, byte[]>(key, value));
        Assert.Single(table);
        Assert.Equal(value, table[key]);
    }

    [Fact]
    public void ICollection_Contains()
    {
        var table = CreateTable(nameof(ICollection_Contains));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        var collection = (ICollection<KeyValuePair<string, byte[]>>)table;
        Assert.True(collection.Contains(new KeyValuePair<string, byte[]>(key, value)));
        Assert.False(collection.Contains(new KeyValuePair<string, byte[]>("nonExistentKey", value)));
    }

    [Fact]
    public void ICollection_CopyTo()
    {
        var table = CreateTable(nameof(ICollection_CopyTo));
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
    public void ICollection_CopyTo_WithInvalidArguments()
    {
        var table = CreateTable(nameof(ICollection_CopyTo_WithInvalidArguments));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        table.Add(key1, value1);
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        table.Add(key2, value2);

        var array1 = new KeyValuePair<string, byte[]>[2];
        Assert.Throws<ArgumentOutOfRangeException>(() => table.CopyTo(array1, -1));

        var array2 = new KeyValuePair<string, byte[]>[1];
        Assert.Throws<ArgumentOutOfRangeException>(() => table.CopyTo(array2, 0));

        var array3 = new KeyValuePair<string, byte[]>[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => table.CopyTo(array3, 3));

        var array4 = new KeyValuePair<string, byte[]>[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => table.CopyTo(array4, 2));
    }

    [Fact]
    public void ICollection_Remove()
    {
        var table = CreateTable(nameof(ICollection_Remove));
        var key = "testKey";
        var value = new byte[] { 1, 2, 3, 4, 5 };
        table.Add(key, value);
        var collection = (ICollection<KeyValuePair<string, byte[]>>)table;
        Assert.True(collection.Remove(new KeyValuePair<string, byte[]>(key, value)));
        Assert.False(collection.Remove(new KeyValuePair<string, byte[]>("nonExistentKey", value)));
        Assert.False(table.ContainsKey(key));
    }

    [Fact]
    public void KeyCollection_Test()
    {
        var table = CreateTable(nameof(KeyCollection_Test));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        var key3 = "key3";
        var value3 = new byte[] { 7, 8, 9 };

        table.Add(key1, value1);
        table.Add(key2, value2);
        table.Add(key3, value3);

        var keys = table.Keys;

        // Count
        Assert.Equal(3, keys.Count);

        // Contains
        Assert.True(keys.Contains(key1));
        Assert.True(keys.Contains(key2));
        Assert.True(keys.Contains(key3));
        Assert.False(keys.Contains("nonExistentKey"));

        // IEnumerable<string>.GetEnumerator
        var keysList = keys.ToList();
        Assert.Equal(3, keysList.Count);
        Assert.Contains(key1, keysList);
        Assert.Contains(key2, keysList);
        Assert.Contains(key3, keysList);

        // IEnumerable.GetEnumerator
        var nonGenericKeys = new List<object>();
        foreach (var key in (IEnumerable)keys)
        {
            nonGenericKeys.Add(key);
        }
        Assert.Equal(3, nonGenericKeys.Count);
        Assert.Contains(key1, nonGenericKeys);
        Assert.Contains(key2, nonGenericKeys);
        Assert.Contains(key3, nonGenericKeys);

        // CopyTo
        var keyArray = new string[3];
        keys.CopyTo(keyArray, 0);
        Assert.Equal(3, keyArray.Length);
        Assert.Contains(key1, keyArray);
        Assert.Contains(key2, keyArray);
        Assert.Contains(key3, keyArray);

        // IsReadOnly
        Assert.True(keys is ICollection<string> collection && collection.IsReadOnly);

        // Exception tests
        Assert.Throws<NotSupportedException>(() => keys.Add("newKey"));
        Assert.Throws<NotSupportedException>(() => keys.Remove(key1));
        Assert.Throws<NotSupportedException>(() => keys.Clear());

        // Remove from table
        table.Remove(key1);
        Assert.Equal(2, keys.Count);
        Assert.False(keys.Contains(key1));
        Assert.True(keys.Contains(key2));
        Assert.True(keys.Contains(key3));

        // Clear table
        table.Clear();
        Assert.Empty(keys);
    }

    [Fact]
    public void KeyCollection_CopyTo_WithInvalidArguments()
    {
        var table = CreateTable(nameof(KeyCollection_CopyTo_WithInvalidArguments));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        table.Add(key1, value1);
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        table.Add(key2, value2);

        var keys = table.Keys;

        var array1 = new string[2];
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.CopyTo(array1, -1));

        var array2 = new string[1];
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.CopyTo(array2, 0));

        var array3 = new string[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.CopyTo(array3, 3));

        var array4 = new string[3];
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.CopyTo(array4, 2));
    }

    [Fact]
    public void KeyCollection_CopyTo()
    {
        var table = CreateTable(nameof(KeyCollection_CopyTo));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        var key3 = "key3";
        var value3 = new byte[] { 7, 8, 9 };

        table.Add(key1, value1);
        table.Add(key2, value2);
        table.Add(key3, value3);

        var keys = table.Keys;

        var exactArray = new string[3];
        keys.CopyTo(exactArray, 0);
        Assert.Equal(3, exactArray.Length);
        Assert.Contains(key1, exactArray);
        Assert.Contains(key2, exactArray);
        Assert.Contains(key3, exactArray);

        var largerArray = new string[5];
        largerArray[3] = "extra1";
        largerArray[4] = "extra2";
        keys.CopyTo(largerArray, 0);
        Assert.Equal(5, largerArray.Length);
        Assert.Contains(key1, largerArray);
        Assert.Contains(key2, largerArray);
        Assert.Contains(key3, largerArray);
        Assert.Equal("extra1", largerArray[3]);
        Assert.Equal("extra2", largerArray[4]);

        var offsetArray = new string[5];
        offsetArray[0] = "before1";
        offsetArray[1] = "before2";
        keys.CopyTo(offsetArray, 2);
        Assert.Equal(5, offsetArray.Length);
        Assert.Equal("before1", offsetArray[0]);
        Assert.Equal("before2", offsetArray[1]);
        Assert.Contains(key1, new[] { offsetArray[2], offsetArray[3], offsetArray[4] });
        Assert.Contains(key2, new[] { offsetArray[2], offsetArray[3], offsetArray[4] });
        Assert.Contains(key3, new[] { offsetArray[2], offsetArray[3], offsetArray[4] });
    }

    [Fact]
    public void ValueCollection_Test()
    {
        var table = CreateTable(nameof(ValueCollection_Test));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        var key3 = "key3";
        var value3 = new byte[] { 7, 8, 9 };
        table.Add(key1, value1);
        table.Add(key2, value2);
        table.Add(key3, value3);
        var values = table.Values;

        // Count
        Assert.Equal(3, values.Count);

        // Contains
        Assert.True(values.Contains(value1));
        Assert.True(values.Contains(value2));
        Assert.True(values.Contains(value3));
        Assert.False(values.Contains(new byte[] { 10, 11, 12 }));

        // IEnumerable<byte[]>.GetEnumerator
        var valuesList = values.ToList();
        Assert.Equal(3, valuesList.Count);
        Assert.Contains(value1, valuesList);
        Assert.Contains(value2, valuesList);
        Assert.Contains(value3, valuesList);

        // IEnumerable.GetEnumerator
        var nonGenericValues = new List<object>();
        foreach (var value in (IEnumerable)values)
        {
            nonGenericValues.Add(value);
        }

        Assert.Equal(3, nonGenericValues.Count);
        Assert.Contains(value1, nonGenericValues);
        Assert.Contains(value2, nonGenericValues);
        Assert.Contains(value3, nonGenericValues);

        // CopyTo
        var valueArray = new byte[3][];
        values.CopyTo(valueArray, 0);
        Assert.Equal(3, valueArray.Length);
        Assert.Contains(value1, valueArray);
        Assert.Contains(value2, valueArray);
        Assert.Contains(value3, valueArray);

        // IsReadOnly
        Assert.True(values is ICollection<byte[]> collection && collection.IsReadOnly);

        // Exception tests
        Assert.Throws<NotSupportedException>(() => values.Add(new byte[] { 10, 11, 12 }));
        Assert.Throws<NotSupportedException>(() => values.Remove(value1));
        Assert.Throws<NotSupportedException>(() => values.Clear());

        // Table modifications
        table.Remove(key1);
        Assert.Equal(2, values.Count);
        Assert.False(values.Contains(value1));
        Assert.True(values.Contains(value2));
        Assert.True(values.Contains(value3));

        // Clear
        table.Clear();
        Assert.Empty(values);
    }

    [Fact]
    public void ValueCollection_CopyTo_WithInvalidArguments()
    {
        var table = CreateTable(nameof(ValueCollection_CopyTo_WithInvalidArguments));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        table.Add(key1, value1);
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        table.Add(key2, value2);

        var values = table.Values;

        var array1 = new byte[2][];
        Assert.Throws<ArgumentOutOfRangeException>(() => values.CopyTo(array1, -1));

        var array2 = new byte[1][];
        Assert.Throws<ArgumentOutOfRangeException>(() => values.CopyTo(array2, 0));

        var array3 = new byte[3][];
        Assert.Throws<ArgumentOutOfRangeException>(() => values.CopyTo(array3, 3));

        var array4 = new byte[3][];
        Assert.Throws<ArgumentOutOfRangeException>(() => values.CopyTo(array4, 2));
    }

    [Fact]
    public void ValueCollection_CopyTo()
    {
        var table = CreateTable(nameof(ValueCollection_CopyTo));
        var key1 = "key1";
        var value1 = new byte[] { 1, 2, 3 };
        var key2 = "key2";
        var value2 = new byte[] { 4, 5, 6 };
        var key3 = "key3";
        var value3 = new byte[] { 7, 8, 9 };

        table.Add(key1, value1);
        table.Add(key2, value2);
        table.Add(key3, value3);

        var values = table.Values;

        var exactArray = new byte[3][];
        values.CopyTo(exactArray, 0);
        Assert.Equal(3, exactArray.Length);
        Assert.Contains(value1, exactArray);
        Assert.Contains(value2, exactArray);
        Assert.Contains(value3, exactArray);

        var largerArray = new byte[5][];
        var extraValue1 = new byte[] { 10, 11, 12 };
        var extraValue2 = new byte[] { 13, 14, 15 };
        largerArray[3] = extraValue1;
        largerArray[4] = extraValue2;
        values.CopyTo(largerArray, 0);
        Assert.Equal(5, largerArray.Length);
        Assert.Contains(value1, largerArray);
        Assert.Contains(value2, largerArray);
        Assert.Contains(value3, largerArray);
        Assert.Equal(extraValue1, largerArray[3]);
        Assert.Equal(extraValue2, largerArray[4]);

        var offsetArray = new byte[5][];
        var beforeValue1 = new byte[] { 16, 17, 18 };
        var beforeValue2 = new byte[] { 19, 20, 21 };
        offsetArray[0] = beforeValue1;
        offsetArray[1] = beforeValue2;
        values.CopyTo(offsetArray, 2);
        Assert.Equal(5, offsetArray.Length);
        Assert.Equal(beforeValue1, offsetArray[0]);
        Assert.Equal(beforeValue2, offsetArray[1]);
        Assert.Contains(value1, new[] { offsetArray[2], offsetArray[3], offsetArray[4] });
        Assert.Contains(value2, new[] { offsetArray[2], offsetArray[3], offsetArray[4] });
        Assert.Contains(value3, new[] { offsetArray[2], offsetArray[3], offsetArray[4] });
    }
}
