using System.IO;
using Libplanet.Net.Messages;
using Libplanet.Serialization;

namespace Libplanet.Net.ModelConverters;

internal sealed class MessageIdModelConverter : ModelConverterBase<MessageId>
{
    protected override MessageId Deserialize(BinaryReader reader, ModelOptions options)
        => new(reader.ReadBytes(MessageId.Size));

    protected override void Serialize(MessageId obj, BinaryWriter writer, ModelOptions options)
        => writer.Write(obj.Bytes.AsSpan());
}
