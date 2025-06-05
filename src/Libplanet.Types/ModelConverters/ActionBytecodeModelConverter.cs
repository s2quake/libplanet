using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Types.ModelConverters;

internal sealed class ActionBytecodeModelConverter : ModelConverterBase<ActionBytecode>
{
    protected override void Serialize(ActionBytecode obj, ref ModelWriter writer, ModelOptions options)
    {
        writer.Write(obj.Bytes.AsSpan());
    }

    protected override ActionBytecode Deserialize(ref ModelReader reader, ModelOptions options)
    {
        var bytes = reader.ReadBytes();
        return new ActionBytecode(bytes);
    }
}
