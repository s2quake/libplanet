using System.IO;
using System.Reflection;

namespace Libplanet.Serialization.DynamicConverters;

internal sealed class ModelScalarConverter : ModelConverterBase<object>
{
    protected override object? Read(BinaryReader reader, Type type, ModelOptions options)
    {
        if (type.GetCustomAttribute<ModelScalarAttribute>() is not { } attribute)
        {
            var message = $"Type '{type}' does not have the {nameof(ModelScalarAttribute)}";
            throw new ArgumentException(message, nameof(type));
        }

        var kind = attribute.Kind;
        object scalarValue = kind switch
        {
            ModelScalarKind.String => reader.ReadString(),
            ModelScalarKind.Boolean => reader.ReadBoolean(),
            ModelScalarKind.Int32 => reader.Read7BitEncodedInt(),
            ModelScalarKind.Int64 => reader.Read7BitEncodedInt64(),
            ModelScalarKind.Hex => reader.ReadBytes(reader.Read7BitEncodedInt()),
            _ => throw new NotSupportedException("The scalar value is of an unsupported type."),
        };
        return ModelScalarUtility.GetObjectFromScalarValue(options, type, scalarValue);
    }

    protected override void Write(BinaryWriter writer, object value, ModelOptions options)
    {
        var scalarValue = ModelScalarUtility.GetScalarValue(value);
        switch (scalarValue)
        {
            case string s:
                writer.Write(s);
                break;
            case bool b:
                writer.Write(b);
                break;
            case int i:
                writer.Write7BitEncodedInt(i);
                break;
            case long l:
                writer.Write7BitEncodedInt64(l);
                break;
            case byte[] bytes:
                writer.Write7BitEncodedInt(bytes.Length);
                writer.Write(bytes);
                break;
            default:
                throw new NotSupportedException("The scalar value is of an unsupported type.");
        }
    }
}
