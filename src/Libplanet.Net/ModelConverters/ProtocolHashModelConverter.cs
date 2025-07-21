using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Net.ModelConverters;

internal sealed class ProtocolHashModelConverter : ModelConverterBase<ProtocolHash>
{
    protected override ProtocolHash Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadBytes(ProtocolHash.Size));

    protected override void Serialize(ProtocolHash obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());
}
