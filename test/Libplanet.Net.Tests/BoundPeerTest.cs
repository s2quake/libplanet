using System.Net;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Tests
{
    public class BoundPeerTest
    {
        [Fact]
        public void Bencoded()
        {
            var expected = new Peer
            {
                Address = new PrivateKey().Address,
                EndPoint = new DnsEndPoint("0.0.0.0", 1234)
            };
            var deserialized = ModelSerializer.Clone(expected);
            Assert.Equal(expected, deserialized);
        }

        [Fact]
        public void ParsePeer()
        {
#pragma warning disable MEN002 // Line is too long
            var peerInfo = "032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233,192.168.0.1,3333";
            var expected = new Peer
            {
                Address = PublicKey.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233").Address,
                EndPoint = new DnsEndPoint("192.168.0.1", 3333)
            };
#pragma warning restore MEN002 // Line is too long
            Assert.Equal(expected, Peer.Parse(peerInfo));
        }

        [Fact]
        public void PeerString()
        {
#pragma warning disable MEN002 // Line is too long
            var expected = "032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233,192.168.0.1,3333";
            var boundPeer = new Peer
            {
                Address = PublicKey.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233").Address,
                EndPoint = new DnsEndPoint("192.168.0.1", 3333)
            };
#pragma warning restore MEN002 // Line is too long
            Assert.Equal(expected, boundPeer.ToString());
        }

        [Fact]
        public void ParsePeerException()
        {
            Assert.Throws<ArgumentException>(() => Peer.Parse(string.Empty));
#pragma warning disable MEN002 // Line is too long
            Assert.Throws<ArgumentException>(() => Peer.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233"));
            Assert.Throws<ArgumentException>(() => Peer.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233,192.168.0.1"));
            Assert.Throws<ArgumentException>(() => Peer.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233,192.168.0.1,999999"));
            Assert.Throws<ArgumentException>(() => Peer.Parse("032038e153d344773986c039ba5dbff12ae70cfdf6ea8beb7c5ea9b361a72a9233,.ninodes.com,31234"));
#pragma warning restore MEN002 // Line is too long
        }

        [Fact]
        public void InvalidHostname()
        {
            Assert.Throws<ArgumentException>(() =>
                new Peer { Address = new PrivateKey().Address, EndPoint = new DnsEndPoint(".ninodes.com", 31234) });
        }
    }
}
