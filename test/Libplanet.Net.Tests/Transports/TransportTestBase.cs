using System.Diagnostics;
using System.Net;
using Libplanet.Net.Messages;
using Libplanet.Net.Tests.Protocols;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Logging;
using Libplanet.Types;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Util;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Transports;

public abstract class TransportTestBase(ITestOutputHelper output)
{
    protected ITransport CreateTransport(
        Random random, ISigner? signer = null, TransportOptions? options = null)
    {
        signer ??= RandomUtility.PrivateKey(random).AsSigner();
        options ??= new TransportOptions();
        if (options.Logger is NullLogger<ITransport>)
        {
            options = options with
            {
                Logger = TestLogging.CreateLogger<ITransport>(output),
            };
    }

        return CreateTransport(signer, options);
    }

    protected abstract ITransport CreateTransport(ISigner signer, TransportOptions transportOptions);

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task StartAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);
        await transport.StartAsync(cancellationToken);
        Assert.True(transport.IsRunning);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(cancellationToken);
        Assert.True(transport.IsRunning);
        await transport.StopAsync(cancellationToken);
        Assert.False(transport.IsRunning);
        Trace.WriteLine("--------------------------------");
        await transport.StartAsync(cancellationToken);
        Assert.True(transport.IsRunning);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task DisposeAsync_Test()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(cancellationToken);
        await transport.StopAsync(cancellationToken);
        await transport.DisposeAsync();

        var peer = RandomUtility.LocalPeer(random);
        var message = new PingMessage();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.StartAsync(cancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.StopAsync(cancellationToken));
        Assert.Throws<ObjectDisposedException>(() => transport.Post(peer, message));

        await transport.DisposeAsync();
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Peer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var host = IPAddress.Loopback.ToString();
        await using var transport = CreateTransport(random, signer);

        var peer1 = transport.Peer;
        Assert.Equal(signer.Address, peer1.Address);
        Assert.Equal(host, peer1.EndPoint.Host);
        Assert.NotEqual(0, peer1.EndPoint.Port);
        await transport.StartAsync(cancellationToken);

        var peer2 = transport.Peer;
        Assert.Equal(signer.Address, peer2.Address);
        Assert.Equal(host, peer2.EndPoint.Host);
        Assert.NotEqual(0, peer2.EndPoint.Port);
        Assert.Equal(peer1, peer2);

        await transport.StopAsync(cancellationToken);

        var peer3 = transport.Peer;
        Assert.Equal(signer.Address, peer3.Address);
        Assert.Equal(host, peer3.EndPoint.Host);
        Assert.NotEqual(0, peer3.EndPoint.Port);
        Assert.Equal(peer2, peer3);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Post()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        var responseTask = transportB.WaitAsync<PingMessage>(cancellationToken);
        var request = transportA.Post(transportB.Peer, new PingMessage());
        var response = await responseTask.WaitAsync(WaitTimeout2, cancellationToken);

        Assert.IsType<PingMessage>(response.Message);
        Assert.Equal(request.Identity, response.Identity);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Post_AfterRestart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        var response1Task = transportB.WaitAsync<PingMessage>(WaitTimeout5, cancellationToken);
        var request1 = transportA.Post(transportB.Peer, new PingMessage());
        var response1 = await response1Task;

        Assert.IsType<PingMessage>(response1.Message);
        Assert.Equal(request1.Identity, response1.Identity);

        await transportA.StopAsync(cancellationToken);
        await transportB.StopAsync(cancellationToken);
        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        var response2Task = transportB.WaitAsync<PingMessage>(WaitTimeout5, cancellationToken);
        var request2 = transportA.Post(transportB.Peer, new PingMessage());
        var response2 = await response2Task;

        Assert.IsType<PingMessage>(response2.Message);
        Assert.Equal(request2.Identity, response2.Identity);

        await transportB.StopAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        var response3Task = transportB.WaitAsync<PingMessage>(WaitTimeout5, cancellationToken);
        var request3 = transportA.Post(transportB.Peer, new PingMessage());
        var response3 = await response3Task;

        Assert.IsType<PingMessage>(response3.Message);
        Assert.Equal(request3.Identity, response3.Identity);

        await transportA.StopAsync(cancellationToken);
        await transportA.StartAsync(cancellationToken);

        var response4Task = transportB.WaitAsync<PingMessage>(WaitTimeout5, cancellationToken);
        var request4 = transportA.Post(transportB.Peer, new PingMessage());
        var response4 = await response4Task;

        Assert.IsType<PingMessage>(response4.Message);
        Assert.Equal(request4.Identity, response4.Identity);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Post_Throw_AfterDisposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(cancellationToken);
        await transport.DisposeAsync();

        var peer = RandomUtility.LocalPeer(random);
        Assert.Throws<ObjectDisposedException>(() => transport.Post(peer, new PingMessage()));
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Post_Throw_NotRunning()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        var peer = RandomUtility.LocalPeer(random);
        Assert.Throws<InvalidOperationException>(() => transport.Post(peer, new PingMessage()));
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendAsync2_MultipleReplies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        transportB.MessageRouter.Register<PingMessage>((m, e) =>
        {
            transportB.Post(e.Sender, new PingMessage(), e.Identity);
            transportB.Post(e.Sender, new PongMessage(), e.Identity);
            transportB.Post(e.Sender, new TestMessage { Data = "1" }, e.Identity);
        });

        static bool Predicate(IMessage message) => message is TestMessage m && m.Data == "1";

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        var messages = await transportA.SendAsync<IMessage>(
            transportB.Peer, new PingMessage(), Predicate, cancellationToken)
            .ToArrayAsync(cancellationToken);

        Assert.Equal(3, messages.Length);

        var message0 = messages.OfType<PingMessage>().First();
        var message1 = messages.OfType<PongMessage>().First();
        var message2 = messages.OfType<TestMessage>().First();

        Assert.IsType<PingMessage>(message0);
        Assert.IsType<PongMessage>(message1);
        var m2 = Assert.IsType<TestMessage>(message2);
        Assert.Equal("1", m2.Data);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendAsync1_Throw_AfterCancel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        using var cancellationTokenSource = new CancellationTokenSource(100);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await transportA.SendAsync<IMessage>(
                transportB.Peer, new PingMessage(), cancellationTokenSource.Token);
        });
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendAsync1_Throw_NoReply()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var transportAOptions = new TransportOptions
        {
            ReplyTimeout = TimeSpan.FromMilliseconds(500),
        };
        await using var transportA = CreateTransport(random, options: transportAOptions);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        await Assert.ThrowsAsync<TimeoutException>(
            () => transportA.SendAsync<IMessage>(transportB.Peer, new PingMessage(), cancellationToken));
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendAsync1_Throw_AfterStop()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        var task = transportA.SendAsync<IMessage>(transportB.Peer, new PingMessage(), cancellationToken);
        await transportA.StopAsync(cancellationToken);
        Assert.False(transportA.IsRunning);

        var e = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.Equal(transportA.StoppingToken, e.CancellationToken);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendAsync2_Throw_AfterCancel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        using var cancellationTokenSource = new CancellationTokenSource(100);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            var message = new PingMessage();
            var predicate = new Func<IMessage, bool>(m => true);
            await transportA.SendAsync(
                transportB.Peer, message, predicate, cancellationTokenSource.Token)
                .ToArrayAsync(cancellationToken);
        });
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendAsync2_Throw_NoReply()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            var message = new PingMessage();
            var predicate = new Func<IMessage, bool>(m => true);
            await transportA.SendAsync(
                transportB.Peer, message, predicate, cancellationToken)
                .ToArrayAsync(cancellationToken);
        });
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendAsync2_Throw_AfterStop()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);
        transportB.MessageRouter.Register<PingMessage>(async (m, e) =>
        {
            while (transportB.IsRunning)
            {
                transportB.Post(e.Sender, new PingMessage(), e.Identity);
                await Task.Delay(100, cancellationToken);
            }
        });

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        var task = Task.Run(async () =>
        {
            var peer = transportB.Peer;
            var message = new PingMessage();
            var predicate = new Func<IMessage, bool>(m => false);
            await foreach (var _ in transportA.SendAsync(peer, message, predicate, cancellationToken))
            {
                // do nothing
            }
        }, cancellationToken);

        await Task.Delay(100, cancellationToken);
        await transportA.StopAsync(cancellationToken);
        Assert.False(transportA.IsRunning);
        var e = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.Equal(transportA.StoppingToken, e.CancellationToken);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendToMany()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var count = 10;
        var transports = new ITransport[count];
        for (var i = 0; i < count; i++)
        {
            transports[i] = CreateTransport(random);
        }

        await transports.AsParallel().ForEachAsync(transport => transport.StartAsync(cancellationToken));

        var sender = RandomUtility.Random(transports);
        var receivers = transports.Where(t => t != sender).ToImmutableArray();

        var peers = receivers.Select(t => t.Peer).ToImmutableArray();

        var waitTasks = receivers.Select(item => item.WaitAsync<TestMessage>(cancellationToken)).ToArray();
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

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendToMany_Throw_AfterDisposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(cancellationToken);
        await transport.DisposeAsync();
        var peers = RandomUtility.Array(random, RandomUtility.LocalPeer).ToImmutableArray();
        var e = Assert.Throws<AggregateException>(() => transport.Post(peers, new PingMessage()));
        Assert.All(e.InnerExceptions, e => Assert.IsType<ObjectDisposedException>(e));
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task SendToMany_Throw_NotRunning()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        var peers = RandomUtility.Array(random, RandomUtility.LocalPeer).ToImmutableArray();
        var e = Assert.Throws<AggregateException>(() => transport.Post(peers, new PingMessage()));
        Assert.All(e.InnerExceptions, e => Assert.IsType<InvalidOperationException>(e));
    }
}
