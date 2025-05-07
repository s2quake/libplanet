using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;

namespace Libplanet.Serialization;

internal sealed record class ModelData : IBencodable
{
    public static readonly byte[] MagicValue = "LPNT"u8.ToArray();
    private const int ElementCount = 3;

    public required ModelHeader Header { get; init; }

    public required IValue Value { get; init; }

    public IValue Bencoded => new List(
        new Binary(MagicValue),
        Header.Bencoded,
        Value);

    public static bool IsData(IValue value)
    {
        if (value is List list
            && list.Count == ElementCount
            && list[0] is Binary binary
            && binary.ByteArray.AsSpan().SequenceEqual(MagicValue))
        {
            return true;
        }

        return false;
    }

    public static bool TryGetData(IValue value, [MaybeNullWhen(false)] out ModelData data)
    {
        if (value is List list
            && list.Count == ElementCount
            && list[0] is Binary binary
            && binary.ByteArray.AsSpan().SequenceEqual(MagicValue)
            && ModelHeader.TryGetHeader(list[1], out var header))
        {
            data = new ModelData
            {
                Header = header,
                Value = list[2],
            };
            return true;
        }

        data = default;
        return false;
    }

    public static ModelData GetData(IValue value)
    {
        if (value is not List list)
        {
            throw new ArgumentException("The value is not a list.", nameof(value));
        }

        if (list.Count != ElementCount)
        {
            throw new ArgumentException("The list does not have two elements.", nameof(value));
        }

        return new ModelData
        {
            Header = ModelHeader.Create(list[1]),
            Value = list[2],
        };
    }

    internal static IValue GetValue(IValue value, string typeName)
    {
        try
        {
            var data = GetData(value);
            if (typeName != data.Header.TypeName)
            {
                throw new ModelSerializationException(
                    $"Given type name {data.Header.TypeName} is not {typeName}");
            }

            return data.Value;
        }
        catch (ModelSerializationException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new ModelSerializationException(e.Message, e);
        }
    }
}
