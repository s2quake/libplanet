using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class PublicKeyModelConverter : ModelConverterBase<PublicKey>
{
    protected override void Serialize(PublicKey obj, ref ModelWriter writer, ModelOptions options)
    {
        writer.Write(obj.Bytes.AsSpan());
    }

    protected override PublicKey Deserialize(ref ModelReader reader, ModelOptions options)
    {
        return new PublicKey(reader.ReadBytes());
    }
}
