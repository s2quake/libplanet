using Bencodex.Types;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;

namespace Libplanet.Tests.Store.Trie.Nodes;

public class ShortNodeTest
{
    [Fact]
    public void ToBencodex()
    {
        var shortNode = new ShortNode
        {
            Key = Nibbles.Parse("beef"),
            Value = new ValueNode { Value = (Text)"foo" },
        };

        var expected =
            new List(
            [
                (Binary)Nibbles.Parse("beef").ByteArray,
                new List([Null.Value, (Text)"foo"]),
            ]);
        var encoded = shortNode.ToBencodex();
        Assert.IsType<List>(encoded);
        Assert.Equal(expected.Count, ((List)encoded).Count);
        Assert.Equal(expected, encoded);
    }
}
