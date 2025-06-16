using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Libplanet.TestUtilities;
using Libplanet.Types;
using NetMQ;
using NetMQ.Sockets;
using Xunit.Abstractions;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Transports;

public abstract class TransportTest(ITestOutputHelper output)
{
    protected const int Timeout = 60 * 1000;

    protected ITransport CreateTransport(
        Random random,
        PrivateKey? privateKey = null,
        ProtocolOptions? protocolOptions = null,
        HostOptions? hostOptions = null)
    {
        return CreateTransport(
            privateKey ?? RandomUtility.PrivateKey(random),
            protocolOptions ?? new ProtocolOptions(),
            hostOptions ?? new HostOptions
            {
                Host = IPAddress.Loopback.ToString(),
            });
    }

    protected abstract ITransport CreateTransport(
        PrivateKey privateKey, ProtocolOptions protocolOptions, HostOptions hostOptions);

    [Fact(Timeout = Timeout)]
    public async Task StartAsync()
    {
        var random = RandomUtility.GetRandom(output);
        using var transport = CreateTransport(random);
        await transport.StartAsync(default);
        Assert.True(transport.IsRunning);
    }

    [Fact(Timeout = Timeout)]
    public async Task RestartAsync()
    {
        var random = RandomUtility.GetRandom(output);
        using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        Assert.True(transport.IsRunning);
        await transport.StopAsync(default);
        Assert.False(transport.IsRunning);
        await transport.StartAsync(default);
        Assert.True(transport.IsRunning);
    }

    [Fact(Timeout = Timeout)]
    public async Task DisposeTest()
    {
        var random = RandomUtility.GetRandom(output);
        using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        await transport.StopAsync(default);
        transport.Dispose();

        var boundPeer = new Peer
        {
            Address = new PrivateKey().Address,
            EndPoint = new DnsEndPoint("127.0.0.1", 1234),
        };
        var message = new PingMessage();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await transport.StartAsync(default));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await transport.StopAsync(default));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await transport.SendMessageAsync(boundPeer, message, default));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await transport.SendMessageAsync(boundPeer, message, 3, default));
        Assert.Throws<ObjectDisposedException>(
            () => transport.BroadcastMessage([], message));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await transport.ReplyMessageAsync(message, Guid.NewGuid(), default));

        // To check multiple Dispose() throws error or not.
        transport.Dispose();
    }

    [Fact(Timeout = Timeout)]
    public async Task Peer()
    {
        var random = RandomUtility.GetRandom(output);
        var privateKey = new PrivateKey();
        var host = IPAddress.Loopback.ToString();
        using var transport = CreateTransport(random, privateKey: privateKey);

        Assert.Throws<InvalidOperationException>(() => transport.Peer);
        await transport.StartAsync(default);

        var peer = transport.Peer;
        Assert.Equal(privateKey.Address, peer.Address);
        Assert.Equal(host, peer.EndPoint.Host);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendMessageAsync()
    {
        var router = new RouterSocket();
        router.Bind("tcp://*:4000");
        var dealer = new DealerSocket();
        dealer.Connect("tcp://127.0.0.1:4000");


        _ = Task.Run(async () =>
        {
            var m = dealer.ReceiveMultipartBytes();
            int qwr = 0;
        });

        _ = Task.Run(async () =>
        {
            router.SendMultipartBytes(new byte[] { 0 });
        });




        await Task.Delay(20000);

        // var random = RandomUtility.GetRandom(output);
        // using var transportA = CreateTransport(random, hostOptions: new HostOptions { Host= IPAddress.Loopback.ToString(), Port = 4000 });
        // using var transportB = CreateTransport(random, hostOptions: new HostOptions { Host= IPAddress.Loopback.ToString(), Port = 4004 });

        // transportB.ProcessMessageHandler.Register(async message =>
        // {
        //     if (message.Message is PingMessage)
        //     {
        //         await transportB.ReplyMessageAsync(new PongMessage(), message.Id, default);
        //     }
        // });


        // await transportA.StartAsync(default);
        // await transportB.StartAsync(default);

        // var message = new PingMessage();
        // var reply = await transportA.SendMessageAsync(transportB.Peer, message, default);

        // Assert.IsType<PongMessage>(reply.Message);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendMessageCancelAsync()
    {
        var random = RandomUtility.GetRandom(output);
        using var transportA = CreateTransport(random);
        using var transportB = CreateTransport(random);
        var cts = new CancellationTokenSource();

        try
        {
            await transportA.StartAsync(default);
            await transportB.StartAsync(default);

            cts.CancelAfter(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await transportA.SendMessageAsync(
                    transportB.Peer,
                    new PingMessage(),
                    cts.Token));
        }
        finally
        {
            await transportA.StopAsync(default);
            await transportB.StopAsync(default);
            transportA.Dispose();
            transportB.Dispose();
            cts.Dispose();
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task SendMessageMultipleRepliesAsync()
    {
        var random = RandomUtility.GetRandom(output);
        using var transportA = CreateTransport(random);
        using var transportB = CreateTransport(random);

        transportB.ProcessMessageHandler.Register(async message =>
        {
            if (message.Message is PingMessage)
            {
                await transportB.ReplyMessageAsync(
                    new PingMessage(),
                    message.Id,
                    default);
                await transportB.ReplyMessageAsync(
                    new PongMessage(),
                    message.Id,
                    default);
            }
        });

        try
        {
            await transportA.StartAsync(default);
            await transportB.StartAsync(default);

            var replies = (await transportA.SendMessageAsync(
                transportB.Peer,
                new PingMessage(),
                2,
                CancellationToken.None)).ToArray();

            Assert.Contains(replies, message => message.Message is PingMessage);
            Assert.Contains(replies, message => message.Message is PongMessage);
        }
        finally
        {
            await transportA.StopAsync(default);
            await transportB.StopAsync(default);
            transportA.Dispose();
            transportB.Dispose();
        }
    }

    // This also tests ITransport.ReplyMessage at the same time.
    [Fact(Timeout = Timeout)]
    public async Task SendMessageAsyncTimeout()
    {
        var random = RandomUtility.GetRandom(output);
        using var transportA = CreateTransport(random);
        using var transportB = CreateTransport(random);

        try
        {
            await transportA.StartAsync(default);
            await transportB.StartAsync(default);

            var e = await Assert.ThrowsAsync<CommunicationException>(
                async () => await transportA.SendMessageAsync(
                    transportB.Peer,
                    new PingMessage(),
                    CancellationToken.None));
            Assert.True(e.InnerException is TimeoutException ie);
        }
        finally
        {
            await transportA.StopAsync(default);
            await transportB.StopAsync(default);
            transportA.Dispose();
            transportB.Dispose();
        }
    }

    [SkippableTheory(Timeout = Timeout)]
    [ClassData(typeof(TransportTestInvalidPeers))]
    public async Task SendMessageToInvalidPeerAsync(Peer invalidPeer)
    {
        var random = RandomUtility.GetRandom(output);
        using var transport = CreateTransport(random);

        try
        {
            await transport.StartAsync(default);
            Task task = transport.SendMessageAsync(
                invalidPeer,
                new PingMessage(),
                default);

            // TcpTransport and NetMQTransport fail for different reasons, i.e.
            // a thrown exception for each case has a different inner exception.
            await Assert.ThrowsAsync<CommunicationException>(async () => await task);
        }
        finally
        {
            await transport.StopAsync(default);
            transport.Dispose();
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task SendMessageAsyncCancelWhenTransportStop()
    {
        var random = RandomUtility.GetRandom(output);
        using var transportA = CreateTransport(random);
        using var transportB = CreateTransport(random);

        try
        {
            await transportA.StartAsync(default);
            await transportB.StartAsync(default);

            Task t = transportA.SendMessageAsync(
                    transportB.Peer,
                    new PingMessage(),
                    CancellationToken.None);

            // For context change
            await Task.Delay(100);

            await transportA.StopAsync(default);
            Assert.False(transportA.IsRunning);
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await t);
            Assert.True(t.IsCanceled);
        }
        finally
        {
            await transportA.StopAsync(default);
            await transportB.StopAsync(default);
            transportA.Dispose();
            transportB.Dispose();
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastMessage()
    {
        var random = RandomUtility.GetRandom(output);
        var address = new PrivateKey().Address;
        ITransport transportA = null;
        ITransport transportB = CreateTransport(
            random, GeneratePrivateKeyOfBucketIndex(address, 0));
        ITransport transportC = CreateTransport(random,
            privateKey: GeneratePrivateKeyOfBucketIndex(address, 1));
        ITransport transportD = CreateTransport(random,
            privateKey: GeneratePrivateKeyOfBucketIndex(address, 2));

        var tcsB = new TaskCompletionSource<MessageEnvelope>();
        var tcsC = new TaskCompletionSource<MessageEnvelope>();
        var tcsD = new TaskCompletionSource<MessageEnvelope>();

        transportB.ProcessMessageHandler.Register(MessageHandler(tcsB));
        transportC.ProcessMessageHandler.Register(MessageHandler(tcsC));
        transportD.ProcessMessageHandler.Register(MessageHandler(tcsD));

        Func<MessageEnvelope, Task> MessageHandler(TaskCompletionSource<MessageEnvelope> tcs)
        {
            return async message =>
            {
                if (message.Message is PingMessage)
                {
                    tcs.SetResult(message);
                }

                await Task.Yield();
            };
        }

        try
        {
            await transportB.StartAsync(default);
            await transportC.StartAsync(default);
            await transportD.StartAsync(default);

            var table = new RoutingTable(address, bucketSize: 1);
            table.AddPeer(transportB.Peer);
            table.AddPeer(transportC.Peer);
            table.AddPeer(transportD.Peer);

            transportA = CreateTransport(random);
            await transportA.StartAsync(default);

            transportA.BroadcastMessage(
                table.PeersToBroadcast(transportD.Peer.Address),
                new PingMessage());

            await Task.WhenAll(tcsB.Task, tcsC.Task);

            Assert.IsType<PingMessage>(tcsB.Task.Result.Message);
            Assert.IsType<PingMessage>(tcsC.Task.Result.Message);
            Assert.False(tcsD.Task.IsCompleted);

            tcsD.SetCanceled();
        }
        finally
        {
            await transportA?.StopAsync(default);
            transportA?.Dispose();
            await transportB.StopAsync(default);
            transportB.Dispose();
            await transportC.StopAsync(default);
            transportC.Dispose();
            await transportD.StopAsync(default);
            transportD.Dispose();
        }
    }

    private class TransportTestInvalidPeers : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            // Make sure the tcp port is invalid.
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
