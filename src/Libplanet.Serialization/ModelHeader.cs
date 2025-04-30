using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;

namespace Libplanet.Serialization;

internal sealed record class ModelHeader : IBencodable
{
    private const int ElementCount = 2;

    public string TypeName { get; init; } = string.Empty;

    public int Version { get; set; }

    public IValue Bencoded => new List(
        new Text(TypeName),
        new Integer(Version));

    public static bool TryGetHeader(
        IValue value, [MaybeNullWhen(false)] out ModelHeader header)
    {
        if (value is List list
            && list.Count == ElementCount
            && list[0] is Text typeName
            && list[1] is Integer version)
        {
            header = new ModelHeader
            {
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
            TypeName = ((Text)list[0]).Value,
            Version = (int)((Integer)list[1]).Value,
        };
    }
}
