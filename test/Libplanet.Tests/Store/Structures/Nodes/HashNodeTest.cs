using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Tests.Store.Structures.Nodes;

public class HashNodeTest
{
    [Fact]
    public void SerializationTest()
    {
        var expectedNode = new HashNode { Hash = RandomUtility.HashDigest<SHA256>() };
        var actualNode = ModelSerializer.Clone(expectedNode);
        Assert.Equal(expectedNode, actualNode);
    }
}
