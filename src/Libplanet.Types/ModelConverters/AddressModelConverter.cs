using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class AddressModelConverter : ModelConverterBase<Address>
{
    protected override Address Deserialize(Stream stream, ModelOptions options)
    {
        var length = Address.Size;
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new Address(bytes.ToArray());
    }

    protected override void Serialize(Address obj, Stream stream, ModelOptions options)
    {
        stream.Write(obj.Bytes.AsSpan());
    }
}
