using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class AddressModelConverter : ModelConverterBase<Address>
{
    protected override void Serialize(Address obj, ref ModelWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());

    protected override Address Deserialize(ref ModelReader reader, ModelOptions options)
        => new(reader.ReadBytes());
}
