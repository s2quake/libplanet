using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class BlockHashModelConverter : ModelConverterBase<BlockHash>
{
    protected override BlockHash Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadBytes(BlockHash.Size));

    protected override void Serialize(BlockHash obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());
}
