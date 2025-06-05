using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class BlockHashModelConverter : ModelConverterBase<BlockHash>
{
    protected override void Serialize(BlockHash obj, ref ModelWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());

    protected override BlockHash Deserialize(ref ModelReader reader, ModelOptions options)
        => new(reader.ReadBytes());
}
