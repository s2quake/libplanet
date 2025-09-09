using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.State.ModelConverters;
using Libplanet.Types;
using LruCacheNet;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;

namespace Libplanet.State.Structures.Nodes;

[ModelConverter(typeof(HashNodeModelConverter), "hnode")]
internal sealed record class HashNode : INode
{
    private const int _cacheSize = 524_288;

    private static readonly LruCache<HashDigest<SHA256>, INode> _cache = new(_cacheSize);

    public required HashDigest<SHA256> Hash { get; init; }

    public required StateIndex StateIndex { get; init; }

    IEnumerable<INode> INode.Children
    {
        get
        {
            yield return Expand();
        }
    }

    public override int GetHashCode() => Hash.GetHashCode();

    public INode Expand()
    {
        if (!TryGetNode(Hash, out var node))
        {
            if (StateIndex.TryGetValue(Hash, out var bytes))
            {
                var context = new ModelOptions
                {
                    Items = ImmutableDictionary<object, object?>.Empty.Add(typeof(StateIndex), StateIndex),
                };
                node = ModelSerializer.DeserializeFromBytes<INode>(bytes, context);
                AddOrUpdate(Hash, node);
            }
            else
            {
                return NullNode.Value;
            }
        }

        return node;
    }

    public static bool TryGetNode(HashDigest<SHA256> hash, [MaybeNullWhen(false)] out INode node)
        => _cache.TryGetValue(hash, out node);

    public static void AddOrUpdate(HashDigest<SHA256> hash, INode node)
        => _cache.AddOrUpdate(hash, node);
}
