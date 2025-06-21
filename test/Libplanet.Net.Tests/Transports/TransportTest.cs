using System.Collections;
using System.Diagnostics;
using System.Net;
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
    public async Task RestartAsync()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        Assert.True(transport.IsRunning);
        await transport.StopAsync(default);
        Assert.False(transport.IsRunning);
        await transport.StartAsync(default);
        Assert.True(transport.IsRunning);
    }

    [Fact(Timeout = Timeout)]
    public async Task DisposeAsyncTest()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        await transport.StopAsync(default);
        await transport.DisposeAsync();

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
        Assert.Throws<ObjectDisposedException>(
            () => transport.BroadcastMessage([], message));
        Assert.Throws<ObjectDisposedException>(
            () => transport.ReplyMessage(Guid.NewGuid(), message));

        // To check multiple Dispose() throws error or not.
        await transport.DisposeAsync();
    }

    [Fact(Timeout = Timeout)]
    public async Task Peer()
    {
        var random = RandomUtility.GetRandom(output);
        var privateKey = new PrivateKey();
        var host = IPAddress.Loopback.ToString();
        await using var transport = CreateTransport(random, privateKey: privateKey);

        Assert.Throws<InvalidOperationException>(() => transport.Peer);
        await transport.StartAsync(default);

        var peer = transport.Peer;
        Assert.Equal(privateKey.Address, peer.Address);
        Assert.Equal(host, peer.EndPoint.Host);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendMessageAsync()
    {
        // using var router = new RouterSocket();
        // using var poller = new NetMQPoller() { router };
        // poller.RunAsync();
        // poller.Stop();

        // return;
        // var id = Guid.NewGuid();
        // var router = new RouterSocket();
        // // router.Options.RouterHandover = true;
        // router.Bind("tcp://*:4000");
        // var dealer = new DealerSocket();
        // // dealer.Options.DisableTimeWait = true;
        // dealer.Options.Identity = id.ToByteArray();
        // dealer.Connect("tcp://127.0.0.1:4000");

        // using var runtime = new NetMQRuntime();
        // var task1 = Task.Run(() =>
        // {
        //     var m = dealer.ReceiveMultipartMessage();
        //     int qwr = 0;
        // });

        // var task2 = Task.Run(() =>
        // {
        //     var m1 = router.ReceiveMultipartMessage();
        //     var m = new NetMQMessage();
        //     m.Push(id.ToByteArray());
        //     m.Append([0, 1]);
        //     router.TrySendMultipartMessage(TimeSpan.FromSeconds(1), m);
        //     int qwr = 0;
        // });

        // dealer.TrySendMultipartMessage(new NetMQMessage([new NetMQFrame([0, 1])]));

        // runtime.Run(task1, task2);

        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        Trace.WriteLine("1");
        using var subscription = transportB.ProcessMessage.Subscribe(messageEnvelope =>
        {
            Trace.WriteLine("12312");
            if (messageEnvelope.Message is PingMessage)
            {
                Thread.Sleep(100); // Simulate some processing delay.
                transportB.ReplyMessage(messageEnvelope.Identity, new PongMessage());
            }
        });


        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Task.Delay(100);

        var message = new PingMessage();
        var reply = await transportA.SendMessageAsync(transportB.Peer, message, default);

        Assert.IsType<PongMessage>(reply.Message);
    }

    [Fact(Timeout = Timeout)]
    public async Task SendMessageCancelAsync()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await transportA.SendMessageAsync(
                transportB.Peer,
                new PingMessage(),
                cancellationTokenSource.Token));
    }

    [Fact(Timeout = Timeout)]
    public async Task SendMessageMultipleRepliesAsync()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        using var subscription = transportB.ProcessMessage.Subscribe(messageEnvelope =>
        {
            if (messageEnvelope.Message is PingMessage)
            {
                var replyMessage = new AggregateMessage
                {
                    Messages =
                    [
                        new PingMessage(),
                        new PongMessage(),
                    ]
                };
                transportB.ReplyMessage(messageEnvelope.Identity, replyMessage);
            }
        });

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var reply = await transportA.SendMessageAsync(transportB.Peer, new PingMessage(), default);
        var replayMessage = Assert.IsType<AggregateMessage>(reply.Message);

        Assert.Contains(replayMessage.Messages, message => message is PingMessage);
        Assert.Contains(replayMessage.Messages, message => message is PongMessage);
    }

    // This also tests ITransport.ReplyMessage at the same time.
    [Fact(Timeout = Timeout)]
    public async Task SendMessageAsyncTimeout()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Assert.ThrowsAsync<TimeoutException>(
            async () => await transportA.SendMessageAsync(transportB.Peer, new PingMessage(), default));
    }

    [SkippableTheory(Timeout = Timeout)]
    [ClassData(typeof(TransportTestInvalidPeers))]
    public async Task SendMessageToInvalidPeerAsync(Peer invalidPeer)
    {
        var random = RandomUtility.GetRandom(output);
        await using var transport = CreateTransport(random);

        await transport.StartAsync(default);
        await Assert.ThrowsAsync<CommunicationException>(
            async () => await transport.SendMessageAsync(invalidPeer, new PingMessage(), default));
    }

    [Fact(Timeout = Timeout)]
    public async Task SendMessageAsyncCancelWhenTransportStop()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var task = transportA.SendMessageAsync(transportB.Peer, new PingMessage(), default);

        await Task.Delay(100);
        await transportA.StopAsync(default);
        Assert.False(transportA.IsRunning);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.True(task.IsCanceled);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastMessage()
    {
        var random = RandomUtility.GetRandom(output);
        var address = new PrivateKey().Address;
        var transportB = CreateTransport(random, GeneratePrivateKeyOfBucketIndex(address, 0));
        var transportC = CreateTransport(random, GeneratePrivateKeyOfBucketIndex(address, 1));
        var transportD = CreateTransport(random, GeneratePrivateKeyOfBucketIndex(address, 2));

        var tcsB = new TaskCompletionSource<MessageEnvelope>();
        var tcsC = new TaskCompletionSource<MessageEnvelope>();
        var tcsD = new TaskCompletionSource<MessageEnvelope>();

        transportB.ProcessMessage.Subscribe(item => MessageHandler(tcsB)(item));
        transportC.ProcessMessage.Subscribe(item => MessageHandler(tcsC)(item));
        transportD.ProcessMessage.Subscribe(item => MessageHandler(tcsD)(item));

        Action<MessageEnvelope> MessageHandler(TaskCompletionSource<MessageEnvelope> tcs)
        {
            return message =>
            {
                if (message.Message is PingMessage)
                {
                    tcs.SetResult(message);
                }
            };
        }

        await transportB.StartAsync(default);
        await transportC.StartAsync(default);
        await transportD.StartAsync(default);

        var table = new RoutingTable(address, bucketSize: 1);
        table.AddPeer(transportB.Peer);
        table.AddPeer(transportC.Peer);
        table.AddPeer(transportD.Peer);

        await using var transportA = CreateTransport(random);
        await transportA.StartAsync(default);

        transportA.BroadcastMessage(
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
