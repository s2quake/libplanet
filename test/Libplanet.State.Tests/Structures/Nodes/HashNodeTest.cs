using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Serialization;
using Libplanet.State.Structures.Nodes;
using Libplanet.TestUtilities;

namespace Libplanet.State.Tests.Structures.Nodes;

public class HashNodeTest
{
    [Fact]
    public void Serialization()
    {
        var stateIndex = new StateIndex();
        var expectedNode = new HashNode
        {
            Hash = RandomUtility.HashDigest<SHA256>(),
            StateIndex = stateIndex,
        };
        var options = new ModelOptions
        {
            Items = ImmutableDictionary<object, object?>.Empty.Add(typeof(StateIndex), stateIndex),
        };
        var actualNode = ModelSerializer.Clone(expectedNode, options);
        Assert.Equal(expectedNode, actualNode);
    }

    [Fact]
    public void Serialization_Throw()
    {
        var stateIndex = new StateIndex();
        var expectedNode = new HashNode
        {
            Hash = RandomUtility.HashDigest<SHA256>(),
            StateIndex = stateIndex,
        };
        Assert.Throws<InvalidOperationException>(() => ModelSerializer.Clone(expectedNode));
    }
}
