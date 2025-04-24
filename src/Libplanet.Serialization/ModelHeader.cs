using System.Diagnostics.CodeAnalysis;
using Bencodex;
using Bencodex.Types;

namespace Libplanet.Serialization;

internal sealed record class ModelHeader : IBencodable
{
    private const int ElementCount = 3;

    public int TypeValue { get; init; }

    public string TypeName { get; init; } = string.Empty;

    public int Version { get; set; }

    public IValue Bencoded => new List(
        new Integer(TypeValue),
        new Text(TypeName),
        new Integer(Version));

    public static bool TryGetHeader(
        IValue value, [MaybeNullWhen(false)] out ModelHeader header)
    {
        if (value is List list
            && list.Count == ElementCount
            && list[0] is Integer magicValue
            && list[1] is Text typeName
            && list[2] is Integer version)
        {
            header = new ModelHeader
            {
                TypeValue = magicValue,
                TypeName = typeName,
                Version = version,
            };
            return true;
        }

        header = default;
        return false;
    }

    public static ModelHeader Create(IValue value)
    {
        if (value is not List list)
        {
            throw new ArgumentException("The value is not a list.", nameof(value));
        }

        if (list.Count != ElementCount)
        {
            throw new ArgumentException("The list does not have two elements.", nameof(value));
        }

        return new ModelHeader
        {
            TypeValue = (int)((Integer)list[0]).Value,
            TypeName = ((Text)list[1]).Value,
            Version = (int)((Integer)list[2]).Value,
        };
    }
}
