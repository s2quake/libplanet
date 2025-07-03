using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Tests.Store;
using Libplanet.Types;

namespace Libplanet.Net.Tests.Consensus;

[Collection("NetMQConfiguration")]
public sealed class GossipTest
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task PublishMessage()
    {
        using var fx = new MemoryRepositoryFixture();
        var received1 = false;
        var received2 = false;
        var key1 = new PrivateKey();
        var key2 = new PrivateKey();
        var receivedEvent = new ManualResetEvent(false);
        var peer1 = CreatePeer(key1, 6001);
        var peer2 = CreatePeer(key2, 6002);
        await using var gossip1 = CreateGossip(key1, 6001, [peer2]);
        await using var gossip2 = CreateGossip(key2, 6002, [peer1]);
        using var s1 = gossip1.ProcessMessage.Subscribe(message =>
        {
            if (message is ConsensusProposalMessage)
            {
                received1 = true;
            }
        });
        using var s2 = gossip2.ProcessMessage.Subscribe(message =>
        {
            if (message is ConsensusProposalMessage)
            {
                received2 = true;
                receivedEvent.Set();
            }
        });
        await gossip1.StartAsync(default);
        await gossip2.StartAsync(default);
        gossip1.PublishMessage(TestUtils.CreateConsensusPropose(fx.Block1, new PrivateKey(), 0));
        receivedEvent.WaitOne();
        Assert.True(received1);
        Assert.True(received2);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddMessage()
    {
        // It has no difference with PublishMessage() test,
        // since two methods only has timing difference.
        using var fx = new MemoryRepositoryFixture();
        var received1 = false;
        var received2 = false;
        var key1 = new PrivateKey();
        var key2 = new PrivateKey();
        var receivedEvent = new ManualResetEvent(false);
        var peer1 = CreatePeer(key1, 6001);
        var peer2 = CreatePeer(key2, 6002);
        await using var gossip1 = CreateGossip(key1, 6001, [peer2]);
        await using var gossip2 = CreateGossip(key2, 6002, [peer1]);
        using var s1 = gossip1.ProcessMessage.Subscribe(message =>
        {
            if (message is ConsensusProposalMessage)
            {
                received1 = true;
            }
        });
        using var s2 = gossip2.ProcessMessage.Subscribe(message =>
        {
            if (message is ConsensusProposalMessage)
            {
                received2 = true;
                receivedEvent.Set();
            }
        });

        await gossip1.StartAsync(default);
        await gossip2.StartAsync(default);
        gossip1.PublishMessage(TestUtils.CreateConsensusPropose(fx.Block1, new PrivateKey(), 0));
        receivedEvent.WaitOne();
        Assert.True(received1);
        Assert.True(received2);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddMessages()
    {
        using var fx = new MemoryRepositoryFixture();
        var received1 = 0;
        var received2 = 0;
        var key1 = new PrivateKey();
        var key2 = new PrivateKey();
        var receivedEvent = new ManualResetEvent(false);
        var peer1 = CreatePeer(key1, 6001);
        var peer2 = CreatePeer(key2, 6002);
        await using var gossip1 = CreateGossip(key1, 6001, [peer2]);
        await using var gossip2 = CreateGossip(key2, 6002, [peer1]);
        using var g1 = gossip1.ProcessMessage.Subscribe(message =>
        {
            if (message is ConsensusProposalMessage)
            {
                received1++;
            }
        });
        using var g2 = gossip2.ProcessMessage.Subscribe(message =>
        {
            if (message is ConsensusProposalMessage)
            {
                received2++;
            }

            if (received2 == 4)
            {
                receivedEvent.Set();
            }
        });

        await gossip1.StartAsync(default);
        await gossip2.StartAsync(default);
        var privateKey = new PrivateKey();
        IMessage[] message =
        [
            TestUtils.CreateConsensusPropose(fx.Block1, privateKey, 0),
            TestUtils.CreateConsensusPropose(fx.Block1, privateKey, 1),
            TestUtils.CreateConsensusPropose(fx.Block1, privateKey, 2),
            TestUtils.CreateConsensusPropose(fx.Block1, privateKey, 3),
        ];

        Parallel.ForEach(message, gossip1.PublishMessage);

        receivedEvent.WaitOne();
        Assert.Equal(4, received1);
        Assert.Equal(4, received2);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeerWithHaveMessage()
    {
        var key1 = new PrivateKey();
        var key2 = new PrivateKey();
        var received = false;
        var receivedEvent = new ManualResetEvent(false);
        await using var transport1 = CreateTransport(key1, 6001);

        using var s = transport1.Process.Subscribe(messageEnvelope =>
        {
            received = true;
            receivedEvent.Set();
        });
        await using var gossip = new Gossip(transport1);
        await using var transport2 = CreateTransport(key2, 6002);

        await gossip.StartAsync(default);
        await transport2.StartAsync(default);
        await transport2.SendAsync(gossip.Peer, new HaveMessage(), default).FirstAsync(default);

        receivedEvent.WaitOne();
        Assert.True(received);
        Assert.Contains(transport2.Peer, gossip.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotBroadcastToSeedPeers()
    {
        var received = false;
        await using var transport = CreateTransport(port: 6001);
        await using var gossip = CreateGossip(seeds: [transport.Peer]);

        transport.Process.Subscribe(messageEnvelope =>
        {
            if (messageEnvelope.Message is HaveMessage)
            {
                received = true;
            }
        });

        await transport.StartAsync(default);
        await gossip.StartAsync(default);
        gossip.PublishMessage(new PingMessage());

        // Wait heartbeat interval * 2.
        await Task.Delay(2 * 1000);
        Assert.False(received);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotSendDuplicateMessageRequest()
    {
        var received = 0;
        void ProcessMessage(MessageEnvelope messageEnvelope)
        {
            if (messageEnvelope.Message is WantMessage)
            {
                received++;
            }
        }

        await using var receiver = CreateGossip();
        await using var sender1 = CreateTransport();
        using var s1 = sender1.Process.Subscribe(ProcessMessage);
        await using var sender2 = CreateTransport();
        using var s2 = sender2.Process.Subscribe(ProcessMessage);

        await receiver.StartAsync(default);
        await sender1.StartAsync(default);
        await sender2.StartAsync(default);
        var msg1 = new PingMessage();
        var msg2 = new PongMessage();
        await sender1.SendAsync(receiver.Peer, new HaveMessage { Ids = [msg1.Id, msg2.Id] }, default).FirstAsync(default);
        await sender2.SendAsync(receiver.Peer, new HaveMessage { Ids = [msg1.Id, msg2.Id] }, default).FirstAsync(default);

        // Wait heartbeat interval * 2.
        await Task.Delay(2 * 1000);
        Assert.Equal(1, received);
    }

    private static Peer CreatePeer(PrivateKey? privateKey = null, int? port = null) => new()
    {
        Address = (privateKey ?? new PrivateKey()).Address,
        EndPoint = new DnsEndPoint("127.0.0.1", port ?? 0),
    };

    private static Gossip CreateGossip(
        PrivateKey? privateKey = null,
        int? port = null,
        ImmutableHashSet<Peer>? validators = null,
        ImmutableHashSet<Peer>? seeds = null)
    {
        var transport = CreateTransport(privateKey, port);
        var options = new GossipOptions
        {
        };
        return new Gossip(transport, seeds ?? [], validators ?? [], options);
    }

    private static NetMQTransport CreateTransport(
        PrivateKey? privateKey = null,
        int? port = null)
    {
        var options = new TransportOptions
        {
            Protocol = TestUtils.Protocol,
            Host = "127.0.0.1",
            Port = port ?? 0,
        };

        privateKey ??= new PrivateKey();

        return new NetMQTransport(privateKey.AsSigner(), options);
    }
}
