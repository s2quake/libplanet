using System.IO;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store.Trie.Nodes;
using Libplanet.Types;

namespace Libplanet.Store.ModelConverters;

internal sealed class HashNodeModelConverter : ModelConverterBase<HashNode>
{
    protected override HashNode Deserialize(Stream stream, ModelContext context)
    {
        var length = HashDigest<SHA256>.Size;
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        var hash = new HashDigest<SHA256>(bytes);
        var table = context.Items.TryGetValue(typeof(ITable), out var value) ? value as ITable : null;
        return new HashNode
        {
            Hash = hash,
            Table = table,
        };
    }

    protected override void Serialize(HashNode obj, Stream stream, ModelContext context)
    {
        stream.Write(obj.Hash.Bytes.AsSpan());
    }
}
