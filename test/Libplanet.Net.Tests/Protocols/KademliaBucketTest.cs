using System.Net;
using System.Threading.Tasks;
using Libplanet.Net.Protocols;
using Libplanet.Types;

namespace Libplanet.Net.Tests.Protocols;

public class KademliaBucketTest
{
    [Fact]
    public async Task BucketTest()
    {
        var bucket = new Bucket(4, new Random());
        var peer1 = new Peer
        {
            Address = new PrivateKey().Address,
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };
        var peer2 = new Peer
        {
            Address = new PrivateKey().Address,
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };
        var peer3 = new Peer
        {
            Address = new PrivateKey().Address,
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };
        var peer4 = new Peer
        {
            Address = new PrivateKey().Address,
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };
        var peer5 = new Peer
        {
            Address = new PrivateKey().Address,
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };

        // Checks for an empty bucket.
        Assert.True(bucket.IsEmpty);
        Assert.False(bucket.IsFull);
        Assert.Empty(bucket.Peers);
        Assert.Empty(bucket.PeerStates);
        Assert.Throws<InvalidOperationException>(() => bucket.GetRandomPeer(default));
        Assert.Null(bucket.Head);
        Assert.Null(bucket.Tail);

        // Checks for a partially filled bucket.
        bucket.AddPeer(peer1, DateTimeOffset.UtcNow);
        Assert.False(bucket.IsEmpty);
        Assert.False(bucket.IsFull);
        Assert.True(bucket.Contains(peer1));
        Assert.False(bucket.Contains(peer2));
        Assert.Equal(peer1, bucket.GetRandomPeer(default));
        Assert.Null(bucket.GetRandomPeer(peer1.Address));
        Assert.NotNull(bucket.GetRandomPeer(peer2.Address));
        Assert.Equal(peer1, bucket.Head?.Peer);
        Assert.Equal(peer1, bucket.Tail?.Peer);

        // Sleep statement is used to distinguish updated times.
        await Task.Delay(100);
        bucket.AddPeer(peer2, DateTimeOffset.UtcNow);
        Assert.Contains(bucket.GetRandomPeer(default), new[] { peer1, peer2 });
        Assert.Contains(bucket.GetRandomPeer(peer1.Address), new[] { peer2 });

        // Checks for a full bucket.
        await Task.Delay(100);
        bucket.AddPeer(peer3, DateTimeOffset.UtcNow);
        await Task.Delay(100);
        bucket.AddPeer(peer4, DateTimeOffset.UtcNow);
        Assert.True(bucket.IsFull);
        Assert.Equal(
            [.. bucket.Peers],
            new HashSet<Peer> { peer1, peer2, peer3, peer4 });
        Assert.Contains(
            bucket.GetRandomPeer(default),
            new[] { peer1, peer2, peer3, peer4 });
        await Task.Delay(100);
        bucket.AddPeer(peer5, DateTimeOffset.UtcNow);
        Assert.Equal(
            [.. bucket.Peers],
            new HashSet<Peer> { peer1, peer2, peer3, peer4 });
        Assert.False(bucket.Contains(peer5));
        Assert.Equal(peer4, bucket.Head?.Peer);
        Assert.Equal(peer1, bucket.Tail?.Peer);

        // Check order has changed.
        await Task.Delay(100);
        bucket.AddPeer(peer1, DateTimeOffset.UtcNow);
        Assert.Equal(peer1, bucket.Head?.Peer);
        Assert.Equal(peer2, bucket.Tail?.Peer);

        Assert.False(bucket.Remove(peer5));
        Assert.True(bucket.Remove(peer1));
        Assert.DoesNotContain(peer1, bucket.Peers);
        Assert.Equal(3, bucket.Peers.Count());

        // Clear the bucket.
        bucket.Clear();
        Assert.True(bucket.IsEmpty);
        Assert.Empty(bucket.Peers);
        Assert.Null(bucket.Head);
        Assert.Null(bucket.Tail);
        Assert.Throws<InvalidOperationException>(() => bucket.GetRandomPeer(default));
    }
}
