using System.Collections.Concurrent;
using System.Security.Cryptography;
using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class StateIndexTestBase<TTable>(ITestOutputHelper output) : IDisposable
    where TTable : ITable
{
    private readonly ConcurrentBag<TTable> _tables = [];
    private bool _disposedValue;

    public TTable CreateTable(string name)
    {
        var table = CreateTableOverride(name);
        _tables.Add(table);
        return table;
    }

    protected abstract TTable CreateTableOverride(string name);

    protected abstract void DeleteTableOverride(TTable table);

    [Fact]
    public void GetTrie()
    {
        var random = RandomUtility.GetRandom(output);
        var table = CreateTable(nameof(GetTrie));
        var stateIndex = new StateIndex(table);
        var trie = stateIndex.GetTrie(default);
        Assert.True(trie.IsEmpty);

        var nonExistentKey = RandomUtility.HashDigest<SHA256>(random);
        Assert.Throws<KeyNotFoundException>(() => stateIndex.GetTrie(nonExistentKey));

        var key = RandomUtility.HashDigest<SHA256>(random);
        table[key.ToString()] = [1, 2, 3, 4];
        var retrievedTrie = stateIndex.GetTrie(key);
        Assert.Equal(key, retrievedTrie.Hash);
    }

    [Fact]
    public void Commit()
    {
        var random = RandomUtility.GetRandom(output);
        var table = CreateTable(nameof(Commit));
        var stateIndex = new StateIndex(table);
        var trie = stateIndex.GetTrie(default);

        Assert.Throws<ArgumentException>(() => stateIndex.Commit(trie));
        Assert.Throws<ArgumentException>(() => stateIndex.Commit(new Trie()));

        var key = RandomUtility.Word(random);
        var value = RandomUtility.String(random);
        trie = trie.Set(key.ToString(), value);
        var hash = trie.Hash;
        var committedTrie = stateIndex.Commit(trie);
        Assert.NotEqual(hash, committedTrie.Hash);
        Assert.IsType<HashNode>(committedTrie.Node);

        Assert.Throws<ArgumentException>(() => stateIndex.Commit(committedTrie));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                foreach (var table in _tables)
                {
                    DeleteTableOverride(table);
                }
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
