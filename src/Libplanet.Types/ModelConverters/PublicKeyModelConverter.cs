using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class PublicKeyModelConverter : ModelConverterBase<PublicKey>
{
    protected override PublicKey Deserialize(BinaryReader reader, ModelOptions options)
    {
        var length = reader.ReadInt32();
        return new PublicKey(reader.ReadBytes(length));
    }

    protected override void Serialize(PublicKey obj, BinaryWriter writer, ModelOptions options)
    {
        var bytes = obj.Bytes.AsSpan();
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
