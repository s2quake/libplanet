using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class AddressModelConverter : ModelConverterBase<Address>
{
    protected override Address Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadBytes(Address.Size));

    protected override void Serialize(Address obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());
}
