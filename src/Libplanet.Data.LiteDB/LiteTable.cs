using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using LiteDB;

namespace Libplanet.Data.LiteDB;

public sealed class LiteTable(global::LiteDB.LiteDatabase db, string name) : TableBase, IDisposable
{
    private readonly ILiteCollection<BsonDocument> _collection = CreateCollection(db, name);

    public override byte[] this[string key]
    {
        get
        {
            var doc = _collection.FindById(new BsonValue(key));
            if (doc is null || !doc.TryGetValue("value", out BsonValue value) || !value.IsBinary)
            {
                throw new KeyNotFoundException($"Key '{key}' not found.");
            }

            return value.AsBinary;
        }

        set
        {
            var doc = new BsonDocument
            {
                ["key"] = key,
                ["value"] = new BsonValue(value),
            };
            _collection.Upsert(doc);
        }
    }

    public override int Count => throw new NotImplementedException();

    public override void Add(string key, byte[] value)
    {
        _collection.Insert(new BsonDocument
        {
            ["key"] = key,
            ["value"] = new BsonValue(value),
        });
    }

    public override void Clear()
    {

    }

    public override bool ContainsKey(string key)
    {
        return _collection.FindById(new BsonValue(key)) is not null;
    }

    public void Dispose()
    {
        db.DropCollection(name);
    }

    public override bool Remove(string key)
    {
        throw new NotImplementedException();
    }

    public override bool TryGetValue(string key, [MaybeNullWhen(false)] out byte[] value)
    {
        throw new NotImplementedException();
    }

    protected override IEnumerable<string> EnumerateKeys()
    {
        foreach (var doc in _collection.FindAll())
        {
            if (doc.TryGetValue("key", out BsonValue keyValue) && keyValue.IsString)
            {
                yield return keyValue.AsString;
            }
        }
    }

    private static ILiteCollection<BsonDocument> CreateCollection(global::LiteDB.LiteDatabase db, string name)
    {
        var collection = db.GetCollection<BsonDocument>(name);
        if (!collection.EnsureIndex("key"))
        {
            throw new InvalidOperationException($"Failed to ensure index for collection '{name}'.");
        }

        return collection;
    }
}
