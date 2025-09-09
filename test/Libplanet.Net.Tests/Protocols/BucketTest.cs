using System.Net;
using Libplanet.TestUtilities;

namespace Libplanet.Net.Tests.Protocols;

public class BucketTest(ITestOutputHelper output)
{
    [Fact]
    public async Task BaseTest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var bucket = new Bucket(4);
        var peer1 = new Peer
        {
            Address = Rand.Address(random),
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };
        var peer2 = new Peer
        {
            Address = Rand.Address(random),
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };
        var peer3 = new Peer
        {
            Address = Rand.Address(random),
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };
        var peer4 = new Peer
        {
            Address = Rand.Address(random),
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };
        var peer5 = new Peer
        {
            Address = Rand.Address(random),
            EndPoint = new DnsEndPoint("0.0.0.0", 1234)
        };

        // Checks for an empty bucket.
        Assert.True(bucket.IsEmpty);
        Assert.False(bucket.IsFull);
        Assert.Empty(bucket.Select(item => item.Peer));
        Assert.Empty(bucket);
        Assert.Throws<InvalidOperationException>(() => bucket.GetRandomPeer(default));
        Assert.Throws<InvalidOperationException>(() => bucket.Newest);
        Assert.Throws<InvalidOperationException>(() => bucket.Oldest);

        // Checks for a partially filled bucket.
        bucket.AddOrUpdate(new() { Peer = peer1, LastUpdated = DateTimeOffset.UtcNow });
        Assert.False(bucket.IsEmpty);
        Assert.False(bucket.IsFull);
        Assert.True(bucket.Contains(peer1));
        Assert.False(bucket.Contains(peer2));
        Assert.Equal(peer1, bucket.GetRandomPeer(default));
        Assert.Throws<InvalidOperationException>(() => bucket.GetRandomPeer(peer1.Address));
        Assert.NotNull(bucket.GetRandomPeer(peer2.Address));
        Assert.Equal(peer1, bucket.Newest.Peer);
        Assert.Equal(peer1, bucket.Oldest.Peer);

        // Sleep statement is used to distinguish updated times.
        await Task.Delay(100, cancellationToken);
        bucket.AddOrUpdate(new() { Peer = peer2, LastUpdated = DateTimeOffset.UtcNow });
        Assert.Contains(bucket.GetRandomPeer(default), new[] { peer1, peer2 });
        Assert.Contains(bucket.GetRandomPeer(peer1.Address), new[] { peer2 });

        // Checks for a full bucket.
        await Task.Delay(100, cancellationToken);
        bucket.AddOrUpdate(new() { Peer = peer3, LastUpdated = DateTimeOffset.UtcNow });
        await Task.Delay(100, cancellationToken);
        bucket.AddOrUpdate(new() { Peer = peer4, LastUpdated = DateTimeOffset.UtcNow });
        Assert.True(bucket.IsFull);
        Assert.Equal(
            [.. bucket.Select(item => item.Peer)],
            [peer1, peer2, peer3, peer4]);
        Assert.Contains(
            bucket.GetRandomPeer(default),
            new[] { peer1, peer2, peer3, peer4 });
        await Task.Delay(100, cancellationToken);
        bucket.AddOrUpdate(new() { Peer = peer5, LastUpdated = DateTimeOffset.UtcNow });
        Assert.Equal(
            [.. bucket.Select(item => item.Peer)],
            [peer1, peer2, peer3, peer4]);
        Assert.False(bucket.Contains(peer5));
        Assert.Equal(peer4, bucket.Newest.Peer);
        Assert.Equal(peer1, bucket.Oldest.Peer);

        // Check order has changed.
        await Task.Delay(100, cancellationToken);
        bucket.AddOrUpdate(new() { Peer = peer1, LastUpdated = DateTimeOffset.UtcNow });
        Assert.Equal(peer1, bucket.Newest?.Peer);
        Assert.Equal(peer2, bucket.Oldest?.Peer);

        Assert.False(bucket.Remove(peer5));
        Assert.True(bucket.Remove(peer1));
        Assert.DoesNotContain(peer1, bucket.Select(item => item.Peer));
        Assert.Equal(3, bucket.Select(item => item.Peer).Count());

        // Clear the bucket.
        bucket.Clear();
        Assert.True(bucket.IsEmpty);
        Assert.Empty(bucket.Select(item => item.Peer));
        Assert.Throws<InvalidOperationException>(() => bucket.Newest);
        Assert.Throws<InvalidOperationException>(() => bucket.Oldest);
        Assert.Throws<InvalidOperationException>(() => bucket.GetRandomPeer(default));
    }
}
