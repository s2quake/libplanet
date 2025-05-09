// using System.Collections.Concurrent;
// using System.Diagnostics.CodeAnalysis;

// namespace Libplanet.Store.Trie;

// public sealed class MemoryDictionary<TKey, TValue> : TableBase<TKey, TValue>
//     where TKey : notnull
//     where TValue : notnull
// {
//     private readonly ConcurrentDictionary<TKey, TValue> _dictionary = new();

//     public override int Count => _dictionary.Count;

//     public override TValue this[TKey key]
//     {
//         get => _dictionary[key];
//         set => _dictionary[key] = value;
//     }

//     public override void Add(TKey key, TValue value)
//     {
//         if (!_dictionary.TryAdd(key, value))
//         {
//             throw new ArgumentException("An item with the same key has already been added.", nameof(key));
//         }
//     }

//     public override void Clear() => _dictionary.Clear();

//     public override bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

//     public override bool Remove(TKey key) => _dictionary.TryRemove(key, out _);

//     public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
//         => _dictionary.TryGetValue(key, out value);

//     protected override IEnumerable<TKey> EnumerateKeys() => _dictionary.Keys;
// }
