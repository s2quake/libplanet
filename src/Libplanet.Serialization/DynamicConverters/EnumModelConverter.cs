using System.IO;
using Libplanet.Serialization.Extensions;

namespace Libplanet.Serialization.DynamicConverters;

internal sealed class EnumModelConverter : ModelConverterBase<object>
{
    public override bool CanConvert(Type type) => type.IsEnum;

    protected override object? Read(BinaryReader reader, Type type, ModelOptions options)
        => reader.ReadEnum(type);

    protected override void Write(BinaryWriter writer, object value, ModelOptions options)
        => writer.WriteEnum(value, value.GetType());
}
