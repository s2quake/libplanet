using System.Text.Json;

namespace Libplanet.Serialization.Json;

internal static class Utf8JsonReaderExtensions
{
    public static void Expect(this ref Utf8JsonReader @this, JsonTokenType type)
    {
        if (@this.TokenType != type)
        {
            throw new JsonException($"Expected {type}, got {@this.TokenType}.");
        }
    }

    public static void Expect(this ref Utf8JsonReader @this, params JsonTokenType[] types)
    {
        if (!types.Contains(@this.TokenType))
        {
            throw new JsonException($"Expected one of {string.Join(", ", types)}, got {@this.TokenType}.");
        }
    }

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

    private static void Next(this ref Utf8JsonReader @this)
    {
        if (!@this.Read())
        {
            throw new JsonException("Expected property name, got EOF.");
        }
    }
}
