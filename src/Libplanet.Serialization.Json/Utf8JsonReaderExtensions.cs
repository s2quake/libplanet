using System.Text.Json;

namespace Libplanet.Serialization.Json;

internal static class Utf8JsonReaderExtensions
{
    // Read + expect helpers
    public static void ReadExpect(this ref Utf8JsonReader r, JsonTokenType type)
    {
        if (!r.Read() || r.TokenType != type)
        {
            throw new JsonException($"Expected {type}, got {r.TokenType}.");
        }
    }

    public static void Expect(this ref Utf8JsonReader r, JsonTokenType type)
    {
        if (r.TokenType != type)
        {
            throw new JsonException($"Expected {type}, got {r.TokenType}.");
        }
    }

    public static void ReadStartObject(this ref Utf8JsonReader r) => r.ReadExpect(JsonTokenType.StartObject);

    public static void ReadEndObject(this ref Utf8JsonReader r) => r.ReadExpect(JsonTokenType.EndObject);

    public static void ReadStartArray(this ref Utf8JsonReader r) => r.ReadExpect(JsonTokenType.StartArray);

    public static void ReadEndArray(this ref Utf8JsonReader r) => r.ReadExpect(JsonTokenType.EndArray);

    public static string ReadString(this ref Utf8JsonReader r, string propertyName)
    {
        r.ReadPropertyName(propertyName);
        r.ReadExpect(JsonTokenType.String);
        return r.GetString() ?? throw new JsonException($"Property '{propertyName}' must be a string.");
    }

    public static int ReadInt32(this ref Utf8JsonReader r, string propertyName)
    {
        r.ReadPropertyName(propertyName);
        r.Read();
        if (r.TokenType != JsonTokenType.Number || !r.TryGetInt32(out var v))
        {
            throw new JsonException($"Property '{propertyName}' must be a number(Int32).");
        }

        return v;
    }

    public static string ReadPropertyName(this ref Utf8JsonReader r)
    {
        r.ReadExpect(JsonTokenType.PropertyName);
        if (r.GetString() is not { } s)
        {
            throw new JsonException("Expected property name, got null.");
        }

        return s;
    }

    public static void ReadPropertyName(this ref Utf8JsonReader r, string propertyName)
    {
        r.ReadExpect(JsonTokenType.PropertyName);
        if (r.GetString() is not { } s)
        {
            throw new JsonException($"Expected property '{propertyName}', got null.");
        }

        if (s != propertyName)
        {
            throw new JsonException($"Expected property '{propertyName}', got '{s}'.");
        }
    }

    // public static void ExpectPropertyName(this ref Utf8JsonReader r, string propertyName)
    // {
    //     r.ReadExpect(JsonTokenType.PropertyName);
    //     if (r.GetString() is not { } s)
    //     {
    //         throw new JsonException($"Expected property '{propertyName}', got null.");
    //     }

    //     if (!string.Equals(s, propertyName, StringComparison.Ordinal))
    //     {
    //         throw new JsonException($"Expected property '{propertyName}', got '{s}'.");
    //     }
    // }

    // Required properties (throws if missing or wrong type)
    public static string ReadRequiredStringProperty(this ref Utf8JsonReader r, string name)
    {
        r.ReadPropertyName(name);
        r.ReadExpect(JsonTokenType.String);
        return r.GetString()!;
    }

    public static int ReadRequiredInt32Property(this ref Utf8JsonReader r, string name)
    {
        r.ReadPropertyName(name);
        r.Read();
        if (r.TokenType != JsonTokenType.Number || !r.TryGetInt32(out var v))
        {
            throw new JsonException($"Property '{name}' must be a number(Int32).");
        }
        return v;
    }

    public static bool TrySkipValue(this ref Utf8JsonReader r)
    {
        // .NET 6+ provides TrySkip()
#if NET6_0_OR_GREATER
        return r.TrySkip();
#else
        // Fallback: naive skip for simple values
        var startDepth = r.CurrentDepth;
        if (r.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            while (r.Read())
            {
                if (r.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    continue;
                if (r.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                    if (r.CurrentDepth == startDepth - 1) return true;
            }
            return false;
        }
        // Primitive value; just advance once
        return r.Read();
#endif
    }

    // Scan current object for a property; positions reader on its value when found
    public static bool TryReadProperty(this ref Utf8JsonReader r, string name)
    {
        var objectDepth = r.CurrentDepth; // assume we're inside an object (at PropertyName or StartObject already read)
        while (r.Read())
        {
            if (r.TokenType == JsonTokenType.PropertyName)
            {
                var prop = r.GetString();
                if (string.Equals(prop, name, StringComparison.Ordinal))
                {
                    r.Read(); // move to value
                    return true;
                }
                // skip other property's value
                if (!r.TrySkipValue()) return false;
            }
            else if (r.TokenType == JsonTokenType.EndObject && r.CurrentDepth < objectDepth)
            {
                break;
            }
        }
        return false;
    }
}
