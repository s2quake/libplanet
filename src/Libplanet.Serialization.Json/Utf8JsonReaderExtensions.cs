using System.Text.Json;

namespace Libplanet.Serialization.Json;

internal static class Utf8JsonReaderExtensions
{
    public static void ReadExpect(this ref Utf8JsonReader @this, JsonTokenType type)
    {
        if (!@this.Read() || @this.TokenType != type)
        {
            throw new JsonException($"Expected {type}, got {@this.TokenType}.");
        }
    }

    public static void Expect(this ref Utf8JsonReader @this, JsonTokenType type)
    {
        if (@this.TokenType != type)
        {
            throw new JsonException($"Expected {type}, got {@this.TokenType}.");
        }
    }

    public static void ReadStartObject(this ref Utf8JsonReader @this)
    {
        @this.Expect(JsonTokenType.StartObject);
        Next(ref @this);
    }

    public static void ReadEndObject(this ref Utf8JsonReader @this)
    {
        @this.Expect(JsonTokenType.EndObject);
        Next(ref @this);
    }

    public static void ReadStartArray(this ref Utf8JsonReader @this) => @this.ReadExpect(JsonTokenType.StartArray);

    public static void ReadEndArray(this ref Utf8JsonReader @this) => @this.ReadExpect(JsonTokenType.EndArray);

    public static string ReadString(this ref Utf8JsonReader @this, string propertyName)
    {
        @this.ReadPropertyName(propertyName);
        if (@this.GetString() is not { } s)
        {
            throw new JsonException($"Property '{propertyName}' must be a string.");
        }

        Next(ref @this);

        return s;
    }

    public static int ReadInt32(this ref Utf8JsonReader @this, string propertyName)
    {
        @this.ReadPropertyName(propertyName);
        var v = @this.GetInt32();
        Next(ref @this);
        return v;
    }

    public static object ReadObject(
        this ref Utf8JsonReader @this, string propertyName, Type type, JsonSerializerOptions options)
    {
        @this.ExpectPropertyName(propertyName);
        var obj = JsonSerializer.Deserialize(ref @this, type, options);
        Next(ref @this);
        return obj!;
    }

    public static string ReadPropertyName(this ref Utf8JsonReader @this)
    {
        @this.Expect(JsonTokenType.PropertyName);
        if (@this.GetString() is not { } s)
        {
            throw new JsonException("Expected property name, got null.");
        }

        Next(ref @this);

        return s;
    }

    public static void ReadPropertyName(this ref Utf8JsonReader @this, string propertyName)
    {
        @this.Expect(JsonTokenType.PropertyName);
        if (@this.GetString() is not { } s)
        {
            throw new JsonException($"Expected property '{propertyName}', got null.");
        }

        if (s != propertyName)
        {
            throw new JsonException($"Expected property '{propertyName}', got '{s}'.");
        }

        Next(ref @this);
    }

    public static void ExpectPropertyName(this ref Utf8JsonReader @this, string propertyName)
    {
        @this.Expect(JsonTokenType.PropertyName);
        if (@this.GetString() is not { } s)
        {
            throw new JsonException($"Expected property '{propertyName}', got null.");
        }

        if (s != propertyName)
        {
            throw new JsonException($"Expected property '{propertyName}', got '{s}'.");
        }
    }

    private static void Next(ref Utf8JsonReader @this)
    {
        if (!@this.Read())
        {
            throw new JsonException("Expected property name, got EOF.");
        }
    }
}
