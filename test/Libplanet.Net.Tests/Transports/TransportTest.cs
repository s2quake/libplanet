#pragma warning disable S1751 // Loops with at most one iteration should be refactored
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Tests.Protocols;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Nethereum.Util;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Transports;

[Collection("NetMQConfiguration")]
public abstract class TransportTest(ITestOutputHelper output)
{
    protected const int Timeout = 60 * 1000;

    protected ITransport CreateTransport(
        Random random, PrivateKey? privateKey = null, TransportOptions? options = null)
    {
        return CreateTransport(
            privateKey ?? RandomUtility.PrivateKey(random),
            options ?? new TransportOptions());
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
        Assert.Throws<ObjectDisposedException>(() => transport.Post(peer, message));

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
    public async Task Post()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var request = transportA.Post(transportB.Peer, new PingMessage());
        var response = await transportB.WaitMessageAsync<PingMessage>(default);

        Assert.IsType<PingMessage>(response.Message);
        Assert.Equal(request.Identity, response.Identity);
    }

    [Fact(Timeout = Timeout)]
    public async Task Post_Throw_AfterDisposed()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        await transport.DisposeAsync();

        var peer = RandomUtility.LocalPeer(random);
        Assert.Throws<ObjectDisposedException>(() => transport.Post(peer, new PingMessage()));
    }

    [Fact(Timeout = Timeout)]
    public async Task Post_Throw_NotRunning()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        var peer = RandomUtility.LocalPeer(random);
        Assert.Throws<InvalidOperationException>(() => transport.Post(peer, new PingMessage()));
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync2_MultipleReplies()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        transportB.MessageHandlers.Add<PingMessage>((m, e) =>
        {
            transportB.Post(e.Sender, new PingMessage(), e.Identity);
            transportB.Post(e.Sender, new PongMessage(), e.Identity);
        });

        static bool Predicate(IMessage message) => message is PongMessage;

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var messages = await transportA.SendAsync<IMessage>(transportB.Peer, new PingMessage(), Predicate, default)
            .ToArrayAsync();

        Assert.IsType<PingMessage>(messages[0]);
        Assert.IsType<PongMessage>(messages[1]);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync1_Throw_AfterCancel()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        using var cancellationTokenSource = new CancellationTokenSource(100);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await transportA.SendAsync<IMessage>(
                transportB.Peer, new PingMessage(), cancellationTokenSource.Token);
        });
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync1_Throw_NoReply()
    {
        var random = RandomUtility.GetRandom(output);
        var transportAOptions = new TransportOptions
        {
            ReplyTimeout = TimeSpan.FromMilliseconds(500),
        };
        await using var transportA = CreateTransport(random, options: transportAOptions);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await transportA.SendAsync<IMessage>(transportB.Peer, new PingMessage(), default);
        });
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync1_Throw_AfterStop()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var task = transportA.SendAsync<IMessage>(transportB.Peer, new PingMessage(), default);
        await transportA.StopAsync(default);
        Assert.False(transportA.IsRunning);

        var e = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.Equal(transportA.StoppingToken, e.CancellationToken);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync2_Throw_AfterCancel()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        using var cancellationTokenSource = new CancellationTokenSource(100);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            var message = new PingMessage();
            var predicate = new Func<IMessage, bool>(m => true);
            await transportA.SendAsync(
                transportB.Peer, message, predicate, cancellationTokenSource.Token).ToArrayAsync();
        });
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync2_Throw_NoReply()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            var message = new PingMessage();
            var predicate = new Func<IMessage, bool>(m => true);
            await transportA.SendAsync(
                transportB.Peer, message, predicate, default).ToArrayAsync();
        });
    }

    [Fact(Timeout = Timeout)]
    public async Task SendAsync2_Throw_AfterStop()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);
        transportB.MessageHandlers.Add<PingMessage>(async (m, e) =>
        {
            while (transportB.IsRunning)
            {
                transportB.Post(e.Sender, new PingMessage(), e.Identity);
                await Task.Delay(100, default);
            }
        });

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var task = Task.Run(async () =>
        {
            var peer = transportB.Peer;
            var message = new PingMessage();
            var predicate = new Func<IMessage, bool>(m => true);
            await foreach (var _ in transportA.SendAsync(peer, message, predicate, default))
            {
                // do nothing
            }
        });

        await Task.Delay(100);
        await transportA.StopAsync(default);
        Assert.False(transportA.IsRunning);
        var e = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.Equal(transportA.StoppingToken, e.CancellationToken);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendToMany()
    {
        var random = RandomUtility.GetRandom(output);
        var count = 10;
        var transports = new ITransport[count];
        for (var i = 0; i < count; i++)
        {
            transports[i] = CreateTransport(random);
        }

        await transports.AsParallel().ForEachAsync(async transport => await transport.StartAsync(default));

        var sender = RandomUtility.Random(transports);
        var receivers = transports.Where(t => t != sender).ToImmutableArray();

        var peers = receivers.Select(t => t.Peer).ToImmutableArray();

        var waitTasks = receivers.Select(item => item.WaitMessageAsync<TestMessage>(default)).ToArray();
        sender.Post(peers, new TestMessage { Data = "Hello, World!" });
        await Task.WhenAll(waitTasks);

        for (var i = 0; i < waitTasks.Length; i++)
        {
            var messageEnvelope = await waitTasks[i];
            var testMessage = Assert.IsType<TestMessage>(messageEnvelope.Message);
            Assert.Equal(sender.Peer, messageEnvelope.Sender);
            Assert.Equal("Hello, World!", testMessage.Data);
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task SendToMany_Throw_AfterDisposed()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        await transport.DisposeAsync();
        var peers = RandomUtility.Array(random, RandomUtility.LocalPeer).ToImmutableArray();
        var e = Assert.Throws<AggregateException>(() => transport.Post(peers, new PingMessage()));
        Assert.All(e.InnerExceptions, e => Assert.IsType<ObjectDisposedException>(e));
    }

    [Fact(Timeout = Timeout)]
    public async Task SendToMany_Throw_NotRunning()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        var peers = RandomUtility.Array(random, RandomUtility.LocalPeer).ToImmutableArray();
        var e = Assert.Throws<AggregateException>(() => transport.Post(peers, new PingMessage()));
        Assert.All(e.InnerExceptions, e => Assert.IsType<InvalidOperationException>(e));
    }
}
