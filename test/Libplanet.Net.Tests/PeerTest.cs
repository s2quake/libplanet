using System.Net;
using System.Reflection;
using Libplanet.Serialization;
using Libplanet.TestUtilities;

namespace Libplanet.Net.Tests;

public sealed class PeerTest(ITestOutputHelper output)
{
    [Fact]
    public void Attribute()
    {
        var attribute = typeof(Peer).GetCustomAttribute<ModelConverterAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("peer", attribute.TypeName);
    }

    [Fact]
    public void SerializeAndDeserialize()
    {
        var random = Rand.GetRandom(output);
        var port = Rand.Int32(random, 0, 65535);
        var expected = new Peer
        {
            Address = Rand.Address(random),
            EndPoint = new DnsEndPoint("0.0.0.0", port),
        };
        var deserialized = ModelSerializer.Clone(expected);
        Assert.Equal(expected, deserialized);
    }

    [Fact]
    public void Parse()
    {
        var random = Rand.GetRandom(output);
        var endPoint = Rand.DnsEndPoint(random);
        var address = Rand.Address(random);
        var text = $"{address},{endPoint.Host},{endPoint.Port}";
        var expected = new Peer
        {
            Address = address,
            EndPoint = endPoint,
        };
        var actual = Peer.Parse(text);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Parse_Throw()
    {
        Assert.Throws<FormatException>(() => Peer.Parse(string.Empty));
        Assert.Throws<FormatException>(
            () => Peer.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233"));
        Assert.Throws<FormatException>(
            () => Peer.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233,192.168.0.1"));
        Assert.Throws<FormatException>(
            () => Peer.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233,192.168.0.1,999999"));
        Assert.Throws<FormatException>(
            () => Peer.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233,.ninodes.com,31234"));
    }

    [Fact]
    public void ToString_Test()
    {
        var random = Rand.GetRandom(output);
        var peer = Rand.Peer(random);
        var expected = $"{peer.Address},{peer.EndPoint.Host},{peer.EndPoint.Port}";
        var actual = peer.ToString();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Address_Validate()
    {
        var random = Rand.GetRandom(output);
        var peer = new Peer
        {
            Address = default,
            EndPoint = Rand.DnsEndPoint(random),
        };
        ModelAssert.Throws(peer, nameof(Peer.Address));
    }

    [Fact]
    public void EndPoint_Validate()
    {
        var random = Rand.GetRandom(output);
        var port = Rand.Port(random);
        var peer = new Peer
        {
            Address = default,
            EndPoint = new DnsEndPoint(".ninodes.com", port),
        };
        ModelAssert.Throws(peer, nameof(Peer.EndPoint));
    }
}
