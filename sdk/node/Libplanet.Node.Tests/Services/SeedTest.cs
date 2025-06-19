using Libplanet.Net;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Node.Options;
using Libplanet.Node.Services;

namespace Libplanet.Node.Tests.Services;

public class SeedTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void Base_Test()
    {
        var privateKey = new RandomPrivateKey();
        using var endPoint = new RandomEndPoint();
        var options = new SeedOptions
        {
            PrivateKey = privateKey.ToString(),
            EndPoint = endPoint.ToString(),
            AppProtocolVersion = privateKey.ToAppProtocolVersion(0).Token,
        };
        var seed = new Seed(options);
        Assert.Equal(endPoint.Host, seed.BoundPeer.EndPoint.Host);
        Assert.Equal(endPoint.Port, seed.BoundPeer.EndPoint.Port);
        Assert.Equal(privateKey.Address, seed.BoundPeer.Address);
        Assert.False(seed.IsRunning);
        Assert.Empty(seed.Peers);
    }

    [Fact]
    public async Task StartAsync_TestAsync()
    {
        var privateKey = new RandomPrivateKey();
        using var endPoint = new RandomEndPoint();
        var options = new SeedOptions
        {
            PrivateKey = privateKey.ToString(),
            EndPoint = endPoint.ToString(),
            AppProtocolVersion = privateKey.ToAppProtocolVersion(0).Token,
        };
        await using var seed = new Seed(options);
        await seed.StartAsync(cancellationToken: default);
        Assert.True(seed.IsRunning);
    }

    [Fact]
    public async Task StartAsync_Twice_ThrowTestAsync()
    {
        var privateKey = new RandomPrivateKey();
        using var endPoint = new RandomEndPoint();
        var options = new SeedOptions
        {
            PrivateKey = privateKey.ToString(),
            EndPoint = endPoint.ToString(),
            AppProtocolVersion = privateKey.ToAppProtocolVersion(0).Token,
        };
        await using var seed = new Seed(options);
        await seed.StartAsync(cancellationToken: default);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await seed.StartAsync(cancellationToken: default);
        });
    }

    [Fact]
    public async Task StopAsync_TestAsync()
    {
        var privateKey = new RandomPrivateKey();
        using var endPoint = new RandomEndPoint();
        var options = new SeedOptions
        {
            PrivateKey = privateKey.ToString(),
            EndPoint = endPoint.ToString(),
            AppProtocolVersion = privateKey.ToAppProtocolVersion(0).Token,
        };
        await using var seed = new Seed(options);
        await seed.StartAsync(cancellationToken: default);
        await seed.StopAsync(cancellationToken: default);
        Assert.False(seed.IsRunning);
    }

    [Fact]
    public async Task StopAsync_NotRunning_ThrowTestAsync()
    {
        var privateKey = new RandomPrivateKey();
        using var endPoint = new RandomEndPoint();
        var options = new SeedOptions
        {
            PrivateKey = privateKey.ToString(),
            EndPoint = endPoint.ToString(),
            AppProtocolVersion = privateKey.ToAppProtocolVersion(0).Token,
        };
        await using var seed = new Seed(options);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await seed.StopAsync(cancellationToken: default);
        });
    }

    [Fact]
    public async Task MessageReceived_TestAsync()
    {
        // Given
        var protocol = Protocol.Create(new(), 0);
        var remotePrivateKey = new RandomPrivateKey();
        using var remoteEndPoint = new RandomEndPoint();
        var remoteBoundPeer = new Net.Peer { Address = remotePrivateKey.Address, EndPoint = remoteEndPoint };
        var transportOptions = new TransportOptions
        {
            Host = remoteEndPoint.Host,
            Port = remoteEndPoint.Port,
            Protocol = protocol,
        };
        await using var transport = new NetMQTransport(remotePrivateKey, transportOptions);

        var seedPrivateKey = new RandomPrivateKey();
        using var seedEndPoint = new RandomEndPoint();
        var seedBoundPeer = new Net.Peer { Address = seedPrivateKey.Address, EndPoint = seedEndPoint };
        var options = new SeedOptions
        {
            PrivateKey = seedPrivateKey.ToString(),
            EndPoint = seedEndPoint.ToString(),
            AppProtocolVersion = protocol.Token,
        };
        await using var seed = new Seed(options);
        await seed.StartAsync(cancellationToken: default);

        // When
        var now = DateTimeOffset.UtcNow;
        var args = await Assert.RaisesAsync<SeedMessageEventArgs>(
            handler => seed.MessageReceived += handler,
            handler => seed.MessageReceived -= handler,
            async () =>
            {
                await transport.SendMessageAsync(
                    seedBoundPeer, new PingMessage(), default);
            });

        // Then
        Assert.IsType<PingMessage>(args.Arguments.Message.Message);
        var peer = seed.Peers.Single();
        Assert.Equal(remoteBoundPeer, peer.BoundPeer);
        Assert.True(now <= peer.LastUpdated);
        Assert.True(now + TimeSpan.FromSeconds(120) <= peer.LifeTime);
        Assert.True(peer.IsAlive);
    }

    [Fact]
    public async Task GetNeighborsMsg_TestAsync()
    {
        // Given
        var protocol = Protocol.Create(new(), 0);
        var length = Random.Shared.Next(3, 10);
        var remotePrivateKeys = new RandomPrivateKey[length];
        var remoteEndPoints = new RandomEndPoint[length];
        var remoteBoundPeers = new Net.Peer[length];

        var transports = new ITransport[length];
        for (var i = 0; i < length; i++)
        {
            var transportOptions = new TransportOptions
            {
                Protocol = protocol,
                Host = remoteEndPoints[i].Host,
                Port = remoteEndPoints[i].Port,
            };
            remotePrivateKeys[i] = new RandomPrivateKey();
            remoteEndPoints[i] = new RandomEndPoint();
            remoteBoundPeers[i] = new Net.Peer { Address = remotePrivateKeys[i].Address, EndPoint = remoteEndPoints[i] };
            transports[i] = new NetMQTransport(remotePrivateKeys[i], transportOptions);
        }

        using var d1 = new DisposerCollection(remoteEndPoints);
        await using var d2 = new AsyncDisposerCollection(transports);

        var seedPrivateKey = new RandomPrivateKey();
        var seedEndPoint = new RandomEndPoint();
        var seedBoundPeer = new Net.Peer { Address = seedPrivateKey.Address, EndPoint = seedEndPoint };
        var options = new SeedOptions
        {
            PrivateKey = seedPrivateKey.ToString(),
            EndPoint = seedEndPoint.ToString(),
            AppProtocolVersion = protocol.Token,
        };
        await using var seed = new Seed(options);
        await seed.StartAsync(cancellationToken: default);

        await Parallel.ForEachAsync(transports, async (transport, _) =>
        {
            await transport.SendMessageAsync(
                seedBoundPeer, new PingMessage(), default);
        });

        // When
        var transport = transports[Random.Shared.Next(length)];
        var replyMessage = await transport.SendMessageAsync(
            seedBoundPeer, new FindNeighborsMessage { }, default);

        // Then
        Assert.Equal(length, seed.Peers.Count);
        Assert.IsType<NeighborsMessage>(replyMessage.Message);
        var neighborsMsg = (NeighborsMessage)replyMessage.Message;
        Assert.Equal(length, neighborsMsg.Found.Length);
    }
}
