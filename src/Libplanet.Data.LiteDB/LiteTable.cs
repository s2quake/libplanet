using System.Diagnostics.CodeAnalysis;
using LiteDB;

namespace Libplanet.Data.LiteDB;

public sealed class LiteTable(global::LiteDB.LiteDatabase db, string name)
    : TableBase($"{name}"), IDisposable
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
                ["_id"] = key,
                ["value"] = new BsonValue(value),
            };
            _collection.Upsert(doc);
        }
    }

    public override int Count => _collection.Count();

    public override void Add(string key, byte[] value)
    {
        try
        {
            _collection.Insert(new BsonDocument
            {
                ["_id"] = key,
                ["value"] = new BsonValue(value),
            });
        }
        catch (LiteException ex) when (ex.ErrorCode == LiteException.INDEX_DUPLICATE_KEY)
        {
            throw new ArgumentException($"Key '{key}' already exists.", nameof(key));
        }
    }

    public override void Clear()
    {
        _collection.DeleteAll();
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
        return _collection.Delete(new BsonValue(key));
    }

    public override bool TryGetValue(string key, [MaybeNullWhen(false)] out byte[] value)
    {
        var doc = _collection.FindById(new BsonValue(key));
        if (doc is not null && doc.TryGetValue("value", out BsonValue bsonValue) && bsonValue.IsBinary)
        {
            value = bsonValue.AsBinary;
            return true;
        }

        value = null;
        return false;
    }

    protected override IEnumerable<string> EnumerateKeys()
    {
        foreach (var doc in _collection.FindAll())
        {
            if (doc.TryGetValue("_id", out BsonValue keyValue) && keyValue.IsString)
            {
                yield return keyValue.AsString;
            }
        }
    }

    private static ILiteCollection<BsonDocument> CreateCollection(global::LiteDB.LiteDatabase db, string name)
    {
        var collection = db.GetCollection<BsonDocument>(name);
        return collection;
    }
}
