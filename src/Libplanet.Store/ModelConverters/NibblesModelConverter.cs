using System.IO;
using Libplanet.Serialization;
using Libplanet.Serialization.Extensions;
using Libplanet.Store.DataStructures;

namespace Libplanet.Store.ModelConverters;

internal sealed class NibblesModelConverter : ModelConverterBase<Nibbles>
{
    protected override Nibbles Deserialize(Stream stream, ModelOptions options)
    {
        var length = stream.ReadInt32();
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new Nibbles([.. bytes]);
    }

    protected override void Serialize(Nibbles obj, Stream stream, ModelOptions options)
    {
        stream.WriteInt32(obj.Length);
        stream.Write(obj.Bytes.AsSpan());
    }
}
