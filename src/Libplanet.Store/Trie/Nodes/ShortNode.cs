using Bencodex.Types;

namespace Libplanet.Store.Trie.Nodes;

internal sealed record class ShortNode(in Nibbles Key, INode Value) : INode
{
    public Nibbles Key { get; } = ValidateKey(Key);

    public INode Value { get; } = ValidateValue(Value);

    IEnumerable<INode> INode.Children => [Value];

    public override int GetHashCode()
    {
        unchecked
        {
            return (Key.GetHashCode() * 397) ^ Value.GetHashCode();
        }
    }

    public IValue ToBencodex() => new List(new Binary(Key.ByteArray), Value.ToBencodex());

    private static Nibbles ValidateKey(in Nibbles key)
    {
        if (key.Length == 0)
        {
            throw new ArgumentException($"Given {nameof(key)} cannot be empty.", nameof(key));
        }

        return key;
    }

    private static INode ValidateValue(INode value)
    {
        if (value is ShortNode)
        {
            var message = $"Given {nameof(value)} cannot be a {nameof(ShortNode)}.";
            throw new ArgumentException(message, nameof(value));
        }

        if (value is HashNode hashNode && hashNode.KeyValueStore is null)
        {
            var message = $"Given {nameof(value)} cannot be a {nameof(HashNode)} " +
                $"without a {nameof(IKeyValueStore)}.";
            throw new ArgumentException(message, nameof(value));
        }

        return value;
    }
}
