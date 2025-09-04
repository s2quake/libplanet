using System.Diagnostics.CodeAnalysis;
using LiteDB;

namespace Libplanet.Data.LiteDB;

public sealed class LiteTable(global::LiteDB.LiteDatabase db, string name)
    : TableBase($"{name}"), IDisposable
{
    private readonly ILiteCollection<BsonDocument> _collection = CreateCollection(db, name);

    public override int Count => _collection.Count();

    public override bool ContainsKey(string key) => _collection.FindById(new BsonValue(key)) is not null;

    public void Dispose() => db.DropCollection(name);

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

    protected override byte[] GetOverride(string key)
    {
        var doc = _collection.FindById(new BsonValue(key));
        if (doc is null || !doc.TryGetValue("value", out BsonValue value) || !value.IsBinary)
        {
            throw new KeyNotFoundException($"Key '{key}' not found.");
        }

        return value.AsBinary;
    }

    protected override void SetOverride(string key, byte[] value)
    {
        var doc = new BsonDocument
        {
            ["_id"] = key,
            ["value"] = new BsonValue(value),
        };
        _collection.Upsert(doc);
    }

    protected override void AddOverride(string key, byte[] value)
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

    protected override bool RemoveOverride(string key) => _collection.Delete(new BsonValue(key));

    protected override void ClearOverride() => _collection.DeleteAll();

    protected override IEnumerable<(string, byte[]?)> EnumerateOverride(bool includeValue)
    {
        foreach (var doc in _collection.FindAll())
        {
            if (!doc.TryGetValue("_id", out var keyValue))
            {
                throw new InvalidOperationException("Document does not have an _id field.");
            }

            if (!keyValue.IsString)
            {
                throw new InvalidOperationException("_id field is not a string.");
            }

            if (includeValue)
            {
                if (!doc.TryGetValue("value", out var value))
                {
                    throw new InvalidOperationException("Document does not have a value field.");
                }
                if (!value.IsBinary)
                {
                    throw new InvalidOperationException("value field is not a byte array.");
                }

                yield return (keyValue.AsString, value.AsBinary);

            }
            else
            {
                yield return (keyValue.AsString, null);
            }
        }
    }

    private static ILiteCollection<BsonDocument> CreateCollection(global::LiteDB.LiteDatabase db, string name)
    {
        var collection = db.GetCollection<BsonDocument>(name);
        return collection;
    }
}
