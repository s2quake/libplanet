using System.Net;
using Libplanet.Net.Protocols;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Protocols;

public sealed class RoutingTableTest(ITestOutputHelper output)
{
    [Fact]
    public void AddSelf()
    {
        var random = RandomUtility.GetRandom(output);
        var owner = RandomUtility.Address(random);
        var table = new RoutingTable(owner);
        var peer = new Peer { Address = owner, EndPoint = new DnsEndPoint("0.0.0.0", 1234) };
        Assert.Throws<ArgumentException>(() => table.AddOrUpdate(peer));
    }

    [Fact]
    public void AddPeer()
    {
        var random = RandomUtility.GetRandom(output);
        var owner = RandomUtility.Address(random);
        var peer1 = RandomUtility.LocalPeer(random);
        var peer2 = RandomUtility.LocalPeer(random);
        var peer3 = RandomUtility.LocalPeer(random);
        var table = new RoutingTable(owner, 1, 2);

        Assert.True(table.AddOrUpdate(peer1));
        Assert.True(table.AddOrUpdate(peer2));
        Assert.False(table.AddOrUpdate(peer3));
        Assert.True(table.AddOrUpdate(peer1));
        Assert.False(table.AddOrUpdate(peer3));
        Assert.Equal([peer2, peer1], table.Peers);
    }

    [Fact]
    public void RemovePeer()
    {
        var random = RandomUtility.GetRandom(output);
        var peer1 = RandomUtility.LocalPeer(random);
        var peer2 = RandomUtility.LocalPeer(random);
        var table = new RoutingTable(peer1.Address, 1, 2);

        Assert.False(table.Remove(peer1));
        Assert.False(table.Remove(peer2));
        table.AddOrUpdate(peer2);
        Assert.True(table.Remove(peer2));
    }

    [Fact]
    public void PeersToBroadcast()
    {
        var (address, addresses) = GeneratePeersDifferentBuckets();
        var table = new RoutingTable(address);
        var peers = addresses
            .Select(address => new Peer { Address = address, EndPoint = new DnsEndPoint("0.0.0.0", 1234) })
            .ToArray();
        Assert.Equal(10, peers.Length);
        for (var i = 0; i < peers.Length; i++)
        {
            var peer = peers[i];
            table.AddOrUpdate(peer);
            Assert.Equal(i / 2, table.Buckets.IndexOf(peer.Address));
        }

        var broadcastCandidate = table.PeersToBroadcast(default, 0);
        Assert.Equal(5, broadcastCandidate.Length);
        Assert.Equal(
            new HashSet<int> { 0, 1, 2, 3, 4 },
            broadcastCandidate.Select(peer => table.Buckets.IndexOf(peer.Address))
                .ToHashSet());

        broadcastCandidate = table.PeersToBroadcast(default, 10);
        Assert.Equal(10, broadcastCandidate.Length);
        Assert.Equal(peers.ToHashSet(), broadcastCandidate.ToHashSet());
    }

    [Fact]
    public void GetStalePeers()
    {
        var (address, addresses) = GeneratePeersDifferentBuckets();
        var table = new RoutingTable(address);
        int peerCount = addresses.Length;
        var peers = addresses
            .Select(address => new Peer { Address = address, EndPoint = new DnsEndPoint("0.0.0.0", 1234) })
            .ToArray();
        for (var i = 0; i < peerCount; i++)
        {
            var peerState = new PeerState
            {
                Peer = peers[i],
                LastUpdated = DateTimeOffset.UtcNow - (i % 2 == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(2)),
            };
            table.AddOrUpdate(peerState);
        }

        Assert.Equal(peerCount, table.Count);
        Assert.Equal(
            Enumerable.Range(0, peerCount / 2).Select(i => peers[(i * 2) + 1]),
            table.GetStalePeers(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void GetStalePeers_InSingleBucket()
    {
        var random = RandomUtility.GetRandom(output);
        var owner = RandomUtility.Address(random);
        var table = new RoutingTable(owner, 1);
        var peers = RandomUtility.Array(random, RandomUtility.LocalPeer, 10);
        for (var i = 0; i < peers.Length; i++)
        {
            var peerState = new PeerState
            {
                Peer = peers[i],
                LastUpdated = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(i),
            };
            table.AddOrUpdate(peerState);
        }

        Assert.Equal(peers.Length, table.Count);

        for (var i = 0; i < peers.Length; i++)
        {
            Assert.Equal(peers[i], table.GetStalePeers(TimeSpan.FromMinutes(1)).First());
            table.AddOrUpdate(peers[i]);
        }

        Assert.Empty(table.GetStalePeers(TimeSpan.FromMinutes(1)));
    }

    private static (Address, Address[]) GeneratePeersDifferentBuckets()
    {
        var publicKey = PublicKey.Parse(
            "031df3534d8dee5d35d26cf41c52629326fc20bf96d49bdd6c4596db491896daf7");
        var publicKeys = new[]
        {
            // Peer 0 is in bucket 0
            PublicKey.Parse("02ac9d719f8dd7fb8d32a61f95b341e232edf7cae330c76bbfc3b988ef664d36b7"),
            // Peer 1 is in bucket 0
            PublicKey.Parse("03a6a0eabb9a025e20b0890a0d5bfdf2168d3af96ac8e19d83265f75ce98a2dade"),
            // Peer 2 is in bucket 1
            PublicKey.Parse("0224973583a36a2b80fdb7aea7e37fc5d3cd97aacc6a83f50eff3f499d6edc0b45"),
            // Peer 3 is in bucket 1
            PublicKey.Parse("02bda770f7a0608135b47e1f07de2802d2a2ec61ae1e93f9416bb065a622b687a6"),
            // Peer 4 is in bucket 2
            PublicKey.Parse("0273472fb28a73fe14a3ec598e7390340bacf5d83c7f4b22165a395d0b2fd9c721"),
            // Peer 5 is in bucket 2
            PublicKey.Parse("035f735b3ea526d6af53d1102a5fe2b3cc95c7caae41dd58e1a35f1b8459ff8ada"),
            // Peer 6 is in bucket 3
            PublicKey.Parse("0365bd28ceeb8b4a1e5636260f36ddd19f140e8e1e48201cdd57de7152266ea95b"),
            // Peer 7 is in bucket 3
            PublicKey.Parse("0241049ccd1ecb8ac2be2c98db628e9ff29b9c3d2d8a3d63f2d1b7a6c3d30c7d8f"),
            // Peer 8 is in bucket 4
            PublicKey.Parse("03384eeffb76caf5e95fccf154b4af1685d648a7c87d4474fffcf15d92a6e9796e"),
            // Peer 9 is in bucket 4
            PublicKey.Parse("024c11057c7e18d604d45b01dbba6c03f06c9545a99850bc8e9b7c65be4197ce5c"),
        };

        return (publicKey.Address, publicKeys.Select(item => item.Address).ToArray());
    }
}
