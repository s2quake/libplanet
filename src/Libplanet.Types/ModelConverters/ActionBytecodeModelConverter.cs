using System.IO;
using Libplanet.Serialization;
using Libplanet.Serialization.Extensions;
using Libplanet.Types;

namespace Libplanet.Types.ModelConverters;

internal sealed class ActionBytecodeModelConverter : ModelConverterBase<ActionBytecode>
{
    protected override ActionBytecode Deserialize(Stream stream, ModelOptions options)
    {
        var length = stream.ReadInt32();
        Span<byte> bytes = stackalloc byte[length];
        if (stream.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new ActionBytecode(bytes);
    }

    protected override void Serialize(ActionBytecode obj, Stream stream, ModelOptions options)
    {
        var span = obj.Bytes.AsSpan();
        stream.WriteInt32(span.Length);
        stream.Write(obj.Bytes.AsSpan());
    }
}
