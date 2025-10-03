using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Libplanet.Serialization.ModelScalarUtility;

namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class ScalarValueJsonConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsDefined(typeof(ModelScalarAttribute));

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var modelOptions = ModelOptionsScope.Current;
        var scalarKind = GetScalarKind(typeToConvert);
        var value = ReadValue(ref reader, scalarKind);

        return GetObjectFromScalarValue(modelOptions, typeToConvert, value);

        static object ReadValue(ref Utf8JsonReader reader, ModelScalarKind scalarKind)
        {
            return scalarKind switch
            {
                ModelScalarKind.String => reader.GetString() ?? throw new JsonException("Expected a string."),
                ModelScalarKind.Boolean => reader.GetBoolean(),
                ModelScalarKind.Int32 => reader.GetInt32(),
                ModelScalarKind.Int64 => reader.GetInt64(),
                ModelScalarKind.Hex
                    => Convert.FromHexString(reader.GetString() ?? throw new JsonException("Expected a string.")),
                _ => throw new NotSupportedException($"The scalar kind '{scalarKind}' is not supported."),
            };
        }
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        var scalarValue = GetScalarValue(value);
        if (scalarValue is string stringValue)
        {
            writer.WriteStringValue(stringValue);
        }
        else if (scalarValue is bool boolValue)
        {
            writer.WriteBooleanValue(boolValue);
        }
        else if (scalarValue is int int32Value)
        {
            writer.WriteNumberValue(int32Value);
        }
        else if (scalarValue is long int64Value)
        {
            writer.WriteNumberValue(int64Value);
        }
        else if (scalarValue is byte[] byteArrayValue)
        {
            writer.WriteStringValue(Convert.ToHexString(byteArrayValue).ToLowerInvariant());
        }
        else
        {
            throw new NotSupportedException($"The scalar value type '{scalarValue.GetType()}' is not supported.");
        }
    }
}
