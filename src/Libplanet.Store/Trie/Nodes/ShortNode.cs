using System.ComponentModel.DataAnnotations;
using System.IO;
using Libplanet.Serialization;

namespace Libplanet.Store.Trie.Nodes;

[Model(Version = 1)]
internal sealed record class ShortNode : INode, IValidatableObject
{
    [Property(0)]
    public required Nibbles Key { get; init; }

    [Property(1)]
    public required INode Value { get; init; }

    IEnumerable<INode> INode.Children => [Value];

    public override int GetHashCode()
    {
        unchecked
        {
            return (Key.GetHashCode() * 397) ^ Value.GetHashCode();
        }
    }

    public byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write([.. Key.ByteArray]);
        writer.Write(Value.Serialize());
        return stream.ToArray();
    }

    // public IValue ToBencodex() => new List(new Binary(Key.ByteArray), Value.ToBencodex());

    // private static Nibbles ValidateKey(in Nibbles key)
    // {
    //     if (key.Length == 0)
    //     {
    //         throw new ArgumentException($"Given {nameof(key)} cannot be empty.", nameof(key));
    //     }

    //     return key;
    // }

    // private static INode ValidateValue(INode value)
    // {
    //     if (value is ShortNode)
    //     {
    //         var message = $"Given {nameof(value)} cannot be a {nameof(ShortNode)}.";
    //         throw new ArgumentException(message, nameof(value));
    //     }

    //     if (value is HashNode hashNode && hashNode.Table is null)
    //     {
    //         var message = $"Given {nameof(value)} cannot be a {nameof(HashNode)} " +
    //             $"without a {nameof(IDictionary<KeyBytes, byte[]>)}.";
    //         throw new ArgumentException(message, nameof(value));
    //     }

    //     return value;
    // }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Key.Length == 0)
        {
            yield return new ValidationResult($"Given {nameof(Key)} cannot be empty.", [nameof(Key)]);
        }

        if (Value is ShortNode)
        {
            yield return new ValidationResult(
                $"Given {nameof(Value)} cannot be a {nameof(ShortNode)}.", [nameof(Value)]);
        }

        if (Value is HashNode hashNode && hashNode.Table is null)
        {
            yield return new ValidationResult(
                $"Given {nameof(Value)} cannot be a {nameof(HashNode)} " +
                $"without a {nameof(IDictionary<KeyBytes, byte[]>)}.",
                [nameof(Value)]);
        }
    }
}
