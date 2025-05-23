using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;

namespace Libplanet.Tests.Store.Trie.Nodes;

public class HashNodeTest
{
    [Fact]
    public void ToBencodex()
    {
        var buf = new byte[128];
        var random = new Random();
        random.NextBytes(buf);
        var hashDigest = HashDigest<SHA256>.Create(buf);

        var expectedNode = new HashNode { Hash = hashDigest };
        var actualNode = ModelSerializer.Clone(expectedNode);
        Assert.Equal(expectedNode.Hash, actualNode.Hash);
        Assert.Equal(expectedNode, actualNode);
    }
}
