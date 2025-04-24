using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;

namespace Libplanet.Serialization;

internal sealed record class ModelData : IBencodable
{
    private const int ElementCount = 2;

    public required ModelHeader Header { get; init; }

    public required IValue Value { get; init; }

    public IValue Bencoded => new List(
        Header.Bencoded,
        Value);

    public static bool TryGetObject(
        IValue value, [MaybeNullWhen(false)] out ModelData obj)
    {
        if (value is List list
            && list.Count == ElementCount
            && ModelHeader.TryGetHeader(list[0], out var header))
        {
            obj = new ModelData
            {
                Header = header,
                Value = list[1],
            };
            return true;
        }

        obj = default;
        return false;
    }

    public static ModelData GetObject(IValue value)
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
            Header = ModelHeader.Create(list[0]),
            Value = list[1],
        };
    }
}
