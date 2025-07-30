using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.NetMQ;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Xunit.Abstractions;
using Libplanet.TestUtilities;

namespace Libplanet.Net.Tests.Consensus;

public sealed class GossipTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task PublishMessage()
    {
        var random = RandomUtility.GetRandom(output);
        using var fx = new MemoryRepositoryFixture();
        var received1 = false;
        var received2 = false;
        var key1 = RandomUtility.PrivateKey(random);
        var key2 = RandomUtility.PrivateKey(random);
        var receivedEvent = new ManualResetEvent(false);
        await using var transport1 = CreateTransport(key1);
        await using var transport2 = CreateTransport(key2);
        await using var gossip1 = CreateGossip(transport1, [transport2.Peer]);
        await using var gossip2 = CreateGossip(transport2, [transport1.Peer]);
        transport1.MessageRouter.Register<ConsensusProposalMessage>(message =>
        {
            received1 = true;
        });
        transport2.MessageRouter.Register<ConsensusProposalMessage>(message =>
        {
            received2 = true;
            receivedEvent.Set();
        });
        await gossip1.StartAsync(default);
        await gossip2.StartAsync(default);
        var message = TestUtils.CreateConsensusPropose(fx.Block1, fx.Proposer, 1);
        gossip1.PublishMessage(message);
        receivedEvent.WaitOne();
        Assert.True(received1);
        Assert.True(received2);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddMessages()
    {
        var random = RandomUtility.GetRandom(output);
        using var fx = new MemoryRepositoryFixture();
        var received1 = 0;
        var received2 = 0;
        var key1 = RandomUtility.PrivateKey(random);
        var key2 = RandomUtility.PrivateKey(random);
        var receivedEvent = new ManualResetEvent(false);
        await using var transport1 = CreateTransport(key1);
        await using var transport2 = CreateTransport(key2);
        await using var gossip1 = CreateGossip(transport1, [transport2.Peer]);
        await using var gossip2 = CreateGossip(transport2, [transport1.Peer]);
        transport1.MessageRouter.Register<ConsensusProposalMessage>(message =>
        {
            received1++;
        });
        transport2.MessageRouter.Register<ConsensusProposalMessage>(message =>
        {
            received2++;
            if (received2 == 4)
            {
                receivedEvent.Set();
            }
        });

        await gossip1.StartAsync(default);
        await gossip2.StartAsync(default);
        IMessage[] message =
        [
            TestUtils.CreateConsensusPropose(fx.Block1, fx.Proposer, 1),
            TestUtils.CreateConsensusPropose(fx.Block2, fx.Proposer, 2),
            TestUtils.CreateConsensusPropose(fx.Block3, fx.Proposer, 3),
            TestUtils.CreateConsensusPropose(fx.Block4, fx.Proposer, 4),
        ];

        Parallel.ForEach(message, gossip1.PublishMessage);

        Assert.True(receivedEvent.WaitOne());
        Assert.Equal(4, received1);
        Assert.Equal(4, received2);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeerWithHaveMessage()
    {
        await using var transport1 = CreateTransport();
        await using var gossip = new Gossip(transport1);
        await using var transport2 = CreateTransport();

        await gossip.StartAsync(default);
        await transport2.StartAsync(default);
        transport2.Post(gossip.Peer, new HaveMessage(), default);
        await transport1.WaitAsync<HaveMessage>(default);
        Assert.Contains(transport2.Peer, gossip.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotBroadcastToSeedPeers()
    {
        var handled = false;
        await using var transport = CreateTransport();
        await using var gossip = CreateGossip(seeds: [transport.Peer]);
        transport.MessageRouter.Register<HaveMessage>(_ =>
        {
            handled = true;
        });

        await transport.StartAsync(default);
        await gossip.StartAsync(default);
        gossip.PublishMessage(new PingMessage());

        // Wait heartbeat interval * 2.
        await Task.Delay(2 * 1000);
        Assert.False(handled);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotSendDuplicateMessageRequest()
    {
        var handled = 0;
        void HandelMessage(IMessage message)
        {
            if (message is WantMessage)
            {
                Interlocked.Increment(ref handled);
            }
        }

        await using var receiver = CreateGossip();
        await using var sender1 = CreateTransport();
        sender1.MessageRouter.Register<IMessage>(HandelMessage);
        await using var sender2 = CreateTransport();
        sender2.MessageRouter.Register<IMessage>(HandelMessage);

        await receiver.StartAsync(default);
        await sender1.StartAsync(default);
        await sender2.StartAsync(default);
        var message1 = new PingMessage();
        var message2 = new PongMessage();
        sender1.Post(receiver.Peer, new HaveMessage { Ids = [message1.Id, message2.Id] });
        sender2.Post(receiver.Peer, new HaveMessage { Ids = [message1.Id, message2.Id] });

        // Wait heartbeat interval * 2.
        await Task.Delay(2 * 1000);
        Assert.Equal(1, handled);
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

    private static Gossip CreateGossip(
        ITransport transport,
        ImmutableHashSet<Peer>? validators = null,
        ImmutableHashSet<Peer>? seeds = null)
    {
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
            Host = "127.0.0.1",
            Port = port ?? 0,
        };

        privateKey ??= new PrivateKey();

        return new NetMQTransport(privateKey.AsSigner(), options);
    }
}
