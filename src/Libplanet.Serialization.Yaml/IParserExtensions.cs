using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml;

internal static class IParserExtensions
{
    public static void ReadExpect(this IParser @this, Type type)
    {
        if (!@this.MoveNext() || @this.Current?.GetType() != type)
        {
            throw new YamlException($"Expected {type}, got {@this.Current?.GetType()}.");
        }
    }

    public static void Expect(this IParser @this, Type type)
    {
        if (@this.Current?.GetType() != type)
        {
            throw new YamlException($"Expected {type}, got {@this.Current?.GetType()}.");
        }
    }

    public static void ReadStartObject(this IParser @this)
    {
        @this.Expect(typeof(MappingStart));
        Next(@this);
    }

    public static void ReadEndObject(this IParser @this)
    {
        @this.Expect(typeof(MappingEnd));
        Next(@this);
    }

    public static void ReadStartArray(this IParser @this)
    {
        @this.Expect(typeof(SequenceStart));
        Next(@this);
    }

    public static void ReadEndArray(this IParser @this)
    {
        @this.Expect(typeof(SequenceEnd));
        Next(@this);
    }

    public static string ReadString(this IParser @this, string propertyName)
    {
        @this.ReadPropertyName(propertyName);
        if (@this.GetString() is not { } s)
        {
            throw new YamlException($"Property '{propertyName}' must be a string.");
        }

        Next(@this);

        return s;
    }

    public static int ReadInt32(this IParser @this, string propertyName)
    {
        @this.ReadPropertyName(propertyName);
        var value = @this.GetInt32();
        Next(@this);
        return value;
    }

    public static string ReadStringValue(this IParser @this)
    {
        if (@this.GetString() is not { } s)
        {
            throw new YamlException("Expected string, got null.");
        }

        Next(@this);
        return s;
    }

    public static int ReadInt32Value(this IParser @this)
    {
        var value = @this.GetInt32();
        Next(@this);
        return value;
    }

    public static long ReadInt64Value(this IParser @this)
    {
        var value = @this.GetInt64();
        Next(@this);
        return value;
    }

    public static bool ReadBooleanValue(this IParser @this)
    {
        var value = @this.GetBoolean();
        Next(@this);
        return value;
    }

    public static byte ReadByteValue(this IParser @this)
    {
        var value = @this.GetByte();
        Next(@this);
        return value;
    }

    public static object ReadObject(
        this IParser @this, string propertyName, Type type, ObjectDeserializer deserializer)
    {
        @this.ExpectPropertyName(propertyName);
        var obj = deserializer(type);
        Next(@this);
        return obj!;
    }

    public static string ReadPropertyName(this IParser @this)
    {
        if (@this.GetString() is not { } s)
        {
            throw new YamlException("Expected property name, got null.");
        }

        Next(@this);

        return s;
    }

    public static void ReadPropertyName(this IParser @this, string propertyName)
    {
        if (@this.GetString() is not { } s)
        {
            throw new YamlException($"Expected property '{propertyName}', got null.");
        }

        if (s != propertyName)
        {
            throw new YamlException($"Expected property '{propertyName}', got '{s}'.");
        }

        Next(@this);
    }

    public static void ExpectPropertyName(this IParser @this, string propertyName)
    {
        if (@this.GetString() is not { } s)
        {
            throw new YamlException($"Expected property '{propertyName}', got null.");
        }

        if (s != propertyName)
        {
            throw new YamlException($"Expected property '{propertyName}', got '{s}'.");
        }
    }

    public static string? GetString(this IParser @this)
        => @this.Current is Scalar scalar ? scalar.Value : null;

    public static int GetInt32(this IParser @this)
    {
        if (@this.Current is Scalar scalar && int.TryParse(scalar.Value, out int value))
        {
            return value;
        }

        throw new YamlException("Expected int, got " + @this.Current?.GetType());
    }

    public static long GetInt64(this IParser @this)
    {
        if (@this.Current is Scalar scalar && long.TryParse(scalar.Value, out long value))
        {
            return value;
        }

        throw new YamlException("Expected long, got " + @this.Current?.GetType());
    }

    public static bool GetBoolean(this IParser @this)
    {
        if (@this.Current is Scalar scalar && bool.TryParse(scalar.Value, out bool value))
        {
            return value;
        }

        throw new YamlException("Expected bool, got " + @this.Current?.GetType());
    }

    public static byte GetByte(this IParser @this)
    {
        if (@this.Current is Scalar scalar && byte.TryParse(scalar.Value, out byte value))
        {
            return value;
        }

        throw new YamlException("Expected byte, got " + @this.Current?.GetType());
    }

    private static void Next(IParser @this)
    {
        if (!@this.MoveNext())
        {
            throw new YamlException("Expected property name, got EOF.");
        }
    }
}
