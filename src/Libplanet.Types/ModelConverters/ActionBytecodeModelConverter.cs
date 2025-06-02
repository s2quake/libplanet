using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class ActionBytecodeModelConverter : ModelConverterBase<ActionBytecode>
{
    protected override ActionBytecode Deserialize(BinaryReader reader, ModelOptions options)
    {
        var length = reader.ReadInt32();
        return new ActionBytecode(reader.ReadBytes(length));
    }

    protected override void Serialize(ActionBytecode obj, BinaryWriter writer, ModelOptions options)
    {
        var span = obj.Bytes.AsSpan();
        writer.Write(span.Length);
        writer.Write(obj.Bytes.AsSpan());
    }
}
