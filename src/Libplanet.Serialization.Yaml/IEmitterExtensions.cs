using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Libplanet.Serialization.Yaml;

internal static class IEmitterExtensions
{
    public static void WriteStartObject(this IEmitter @this) =>
        @this.Emit(new MappingStart(AnchorName.Empty, TagName.Empty, isImplicit: true, MappingStyle.Block));

    public static void WriteEndObject(this IEmitter @this) =>
        @this.Emit(new MappingEnd());

    public static void WriteStartArray(this IEmitter @this) =>
        @this.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, isImplicit: true, SequenceStyle.Block));

    public static void WriteEndArray(this IEmitter @this) =>
        @this.Emit(new SequenceEnd());

    public static void WriteString(this IEmitter @this, string propertyName, string value)
    {
        @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, propertyName));
        @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, value));
    }

    public static void WriteNumber(this IEmitter @this, string propertyName, int value)
    {
        @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, propertyName));
        @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, $"{value}"));
    }

    public static void WritePropertyName(this IEmitter @this, string propertyName) =>
        @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, propertyName));

    public static void WriteNullValue(this IEmitter @this)
        => @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, "null"));

    public static void WriteStringValue(this IEmitter @this, string value)
        => @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, value));

    public static void WriteNumberValue(this IEmitter @this, int value)
        => @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, $"{value}"));

    public static void WriteNumberValue(this IEmitter @this, long value)
        => @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, $"{value}"));

    public static void WriteBooleanValue(this IEmitter @this, bool value)
        => @this.Emit(new Scalar(AnchorName.Empty, TagName.Empty, $"{value}".ToLowerInvariant()));
}
