#pragma warning disable S1751 // Loops with at most one iteration should be refactored
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Abstractions;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Transports;

[Collection("NetMQConfiguration")]
public abstract class TransportTest(ITestOutputHelper output)
{
    protected const int Timeout = 60 * 1000;

    protected ITransport CreateTransport(
        Random random, PrivateKey? privateKey = null, TransportOptions? transportOptions = null)
    {
        return CreateTransport(
            privateKey ?? RandomUtility.PrivateKey(random),
            transportOptions ?? new TransportOptions());
    }

    protected abstract ITransport CreateTransport(PrivateKey privateKey, TransportOptions transportOptions);

    [Fact(Timeout = Timeout)]
    public async Task StartAsync()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);
        await transport.StartAsync(default);
        Assert.True(transport.IsRunning);
    }

    [Fact(Timeout = Timeout)]
    public async Task Restart()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        Assert.True(transport.IsRunning);
        await transport.StopAsync(default);
        Assert.False(transport.IsRunning);
        Trace.WriteLine("--------------------------------");
        await transport.StartAsync(default);
        Assert.True(transport.IsRunning);
    }

    [Fact(Timeout = Timeout)]
    public async Task DisposeAsync_Test()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        await transport.StopAsync(default);
        await transport.DisposeAsync();

        var peer = RandomUtility.LocalPeer(random);
        var message = new PingMessage();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await transport.StartAsync(default));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await transport.StopAsync(default));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            transport.Send(peer, message);
            // await foreach (var _ in transport.SendAsync(peer, message, default))
            // {
            //     throw new UnreachableException("This should not be reached.");
            // }
        });
        Assert.Throws<ObjectDisposedException>(() => transport.Send([], message));
        // Assert.Throws<ObjectDisposedException>(() => transport.Reply(Guid.NewGuid(), message));

        await transport.DisposeAsync();
    }

    [Fact(Timeout = Timeout)]
    public async Task Peer()
    {
        var random = RandomUtility.GetRandom(output);
        var privateKey = new PrivateKey();
        var host = IPAddress.Loopback.ToString();
        await using var transport = CreateTransport(random, privateKey: privateKey);

        var peer1 = transport.Peer;
        Assert.Equal(privateKey.Address, peer1.Address);
        Assert.Equal(host, peer1.EndPoint.Host);
        Assert.NotEqual(0, peer1.EndPoint.Port);
        await transport.StartAsync(default);

        var peer2 = transport.Peer;
        Assert.Equal(privateKey.Address, peer2.Address);
        Assert.Equal(host, peer2.EndPoint.Host);
        Assert.NotEqual(0, peer2.EndPoint.Port);
        Assert.Equal(peer1, peer2);

        await transport.StopAsync(default);

        var peer3 = transport.Peer;
        Assert.Equal(privateKey.Address, peer3.Address);
        Assert.Equal(host, peer3.EndPoint.Host);
        Assert.NotEqual(0, peer3.EndPoint.Port);
        Assert.Equal(peer2, peer3);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        transportB.MessageHandlers.Add<PingMessage>(async (m, e) =>
        {
            await Task.Delay(100);
            transportB.Send(e.Sender, new PongMessage(), e.Identity);
        });


        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Task.Delay(100);

        var message = new PingMessage();
        var replyMessage = await transportA.SendForSingleAsync<PongMessage>(transportB.Peer, message, default);

        Assert.IsType<PongMessage>(replyMessage);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync_Cancel()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var cancellationToken = cancellationTokenSource.Token;

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            transportA.Send(transportB.Peer, new PingMessage());
            // await foreach (var _ in transportA.SendAsync(transportB.Peer, new PingMessage(), cancellationToken))
            // {
            //     throw new UnreachableException("This should not be reached.");
            // }
        });
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync_MultipleReplies()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        transportB.MessageHandlers.Add<PingMessage>((m, e) =>
        {
            transportB.Send(e.Sender, new PingMessage(), e.Identity);
            transportB.Send(e.Sender, new PongMessage(), e.Identity);
        });

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        transportA.Send(transportB.Peer, new PingMessage());

        // Assert.IsType<PingMessage>(replyMessages[0]);
        // Assert.IsType<PongMessage>(replyMessages[1]);
    }

    // This also tests ITransport.ReplyMessage at the same time.
    [Fact(Timeout = Timeout)]
    public async Task SendAsync_Timeout()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            transportA.Send(transportB.Peer, new PingMessage());
            // await foreach (var _ in transportA.SendAsync(transportB.Peer, new PingMessage(), default))
            // {
            //     throw new UnreachableException("This should not be reached.");
            // }
        });
    }

    [SkippableTheory(Timeout = Timeout)]
    [ClassData(typeof(TransportTestInvalidPeers))]
    public async Task SendAsync_ToInvalidPeerAsync(Peer invalidPeer)
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            transport.Send(invalidPeer, new PingMessage());
            // await foreach (var _ in transport.SendAsync(invalidPeer, new PingMessage(), default))
            // {
            //     throw new UnreachableException("This should not be reached.");
            // }
        });
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync_CancelWhenTransportStop()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);
        // using var _ = transportB.Process.Subscribe(async replyContext =>
        // {
        //     while (true)
        //     {
        //         replyContext.NextAsync(new PingMessage());
        //     }
        // });

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        // var task = Task.Run(async () =>
        // {
        //     await foreach (var _ in transportA.SendAsync(transportB.Peer, new PingMessage(), default))
        //     {
        //         // do nothing
        //     }
        // });

        await Task.Delay(100);
        await transportA.StopAsync(default);
        Assert.False(transportA.IsRunning);
        // await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
    }

    [Fact(Timeout = Timeout)]
    public async Task Broadcast()
    {
        var random = RandomUtility.GetRandom(output);
        var address = RandomUtility.Address(random);
        var transportB = CreateTransport(random, GeneratePrivateKeyOfBucketIndex(address, 0));
        var transportC = CreateTransport(random, GeneratePrivateKeyOfBucketIndex(address, 1));
        var transportD = CreateTransport(random, GeneratePrivateKeyOfBucketIndex(address, 2));

        var tcsB = new TaskCompletionSource<IReplyContext>();
        var tcsC = new TaskCompletionSource<IReplyContext>();
        var tcsD = new TaskCompletionSource<IReplyContext>();

        // transportB.Process.Subscribe(item => MessageHandler(tcsB)(item));
        // transportC.Process.Subscribe(item => MessageHandler(tcsC)(item));
        // transportD.Process.Subscribe(item => MessageHandler(tcsD)(item));

        Action<IReplyContext> MessageHandler(TaskCompletionSource<IReplyContext> tcs)
        {
            return messageEnvelope =>
            {
                if (messageEnvelope.Message is PingMessage)
                {
                    tcs.SetResult(messageEnvelope);
                }
            };
        }

        await transportB.StartAsync(default);
        await transportC.StartAsync(default);
        await transportD.StartAsync(default);

        var table = new RoutingTable(address);
        table.AddOrUpdate(transportB.Peer);
        table.AddOrUpdate(transportC.Peer);
        table.AddOrUpdate(transportD.Peer);

        await using var transportA = CreateTransport(random);
        await transportA.StartAsync(default);

        transportA.Send(
            table.PeersToBroadcast(transportD.Peer.Address),
            new PingMessage());

        var results = await Task.WhenAll(tcsB.Task, tcsC.Task);

        Assert.IsType<PingMessage>(results[0].Message);
        Assert.IsType<PingMessage>(results[1].Message);
        Assert.False(tcsD.Task.IsCompleted);

        tcsD.SetCanceled();
    }

    private class TransportTestInvalidPeers : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();

            yield return new[]
            {
                new Peer
                {
                    Address = new PrivateKey().Address,
                    EndPoint = new DnsEndPoint("0.0.0.0", port),
                },
            };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
