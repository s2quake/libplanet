using System.IO;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.ModelConverters;

internal sealed class BlockHashModelConverter : ModelConverterBase<BlockHash>
{
    protected override BlockHash Deserialize(Stream stream, ModelContext context)
    {
        var length = BlockHash.Size;
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new BlockHash(bytes.ToArray());
    }

    protected override void Serialize(BlockHash obj, Stream stream, ModelContext context)
        => stream.Write(obj.Bytes.AsSpan());
}
