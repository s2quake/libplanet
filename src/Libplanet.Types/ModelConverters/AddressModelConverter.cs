using System.IO;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.ModelConverters;

internal sealed class AddressModelConverter : ModelConverterBase<Address>
{
    protected override Address Deserialize(Stream stream)
    {
        var length = Address.Size;
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new Address(bytes.ToArray());
    }

    protected override void Serialize(Address obj, Stream stream)
    {
        stream.Write(obj.Bytes.AsSpan());
    }
}
