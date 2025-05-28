using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Data;
using Libplanet.Types.Tests;

namespace Libplanet.Data.Tests.Structures.Nodes;

public class HashNodeTest
{
    [Fact]
    public void SerializationTest()
    {
        var table = new MemoryTable();
        var expectedNode = new HashNode
        {
            Hash = RandomUtility.HashDigest<SHA256>(),
            Table = table,
        };
        var options = new ModelOptions
        {
            Items = ImmutableDictionary<object, object?>.Empty.Add(typeof(ITable), table),
        };
        var actualNode = ModelSerializer.Clone(expectedNode, options);
        Assert.Equal(expectedNode, actualNode);
    }

    [Fact]
    public void SerializationThrowTest()
    {
        var table = new MemoryTable();
        var expectedNode = new HashNode
        {
            Hash = RandomUtility.HashDigest<SHA256>(),
            Table = table,
        };
        Assert.Throws<InvalidOperationException>(() => ModelSerializer.Clone(expectedNode));
    }
}
