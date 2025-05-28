using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class BlockHashModelConverter : ModelConverterBase<BlockHash>
{
    protected override BlockHash Deserialize(Stream stream, ModelOptions options)
    {
        var length = BlockHash.Size;
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new BlockHash(bytes.ToArray());
    }

    protected override void Serialize(BlockHash obj, Stream stream, ModelOptions options)
        => stream.Write(obj.Bytes.AsSpan());
}
