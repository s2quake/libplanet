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
using Libplanet.Net.Components;

namespace Libplanet.Net.Tests.Consensus;

public sealed class GossipTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task PublishMessage()
    {
        using var fx = new MemoryRepositoryFixture();
        var received1 = false;
        var received2 = false;
        var receivedEvent = new ManualResetEvent(false);
        await using var transport1 = TestUtils.CreateTransport();
        await using var transport2 = TestUtils.CreateTransport();
        var peers1 = new PeerCollection(transport1.Peer.Address)
        {
            transport2.Peer,
        };
        var peers2 = new PeerCollection(transport2.Peer.Address)
        {
            transport1.Peer,
        };
        using var gossip1 = new Gossip(transport1, peers1);
        using var gossip2 = new Gossip(transport2, peers2);
        await using var transports = new ServiceCollection
        {
            transport1,
            transport2,
        };
        transport1.MessageRouter.Register<ConsensusProposalMessage>(message =>
        {
            received1 = true;
        });
        transport2.MessageRouter.Register<ConsensusProposalMessage>(message =>
        {
            received2 = true;
            receivedEvent.Set();
        });
        await transports.StartAsync(default);
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
        await using var transport1 = TestUtils.CreateTransport(key1);
        await using var transport2 = TestUtils.CreateTransport(key2);
        var peers1 = new PeerCollection(transport1.Peer.Address)
        {
            transport2.Peer,
        };
        var peers2 = new PeerCollection(transport2.Peer.Address)
        {
            transport1.Peer,
        };
        using var gossip1 = new Gossip(transport1, peers1);
        using var gossip2 = new Gossip(transport2, peers2);
        await using var transports = new ServiceCollection
        {
            transport1,
            transport2,
        };
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

        await transports.StartAsync(default);
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
        var transport1 = TestUtils.CreateTransport();
        var transport2 = TestUtils.CreateTransport();
        using var gossip1 = new Gossip(transport1);
        await using var transports = new ServiceCollection
        {
            transport1,
            transport2,
        };

        await transports.StartAsync(default);

        transport2.Post(gossip1.Peer, new HaveMessage(), default);
        await transport1.WaitAsync<HaveMessage>(default);
        Assert.Contains(transport2.Peer, gossip1.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotBroadcastToSeedPeers()
    {
        var handled = false;
        var transport1 = TestUtils.CreateTransport();
        var transport2 = TestUtils.CreateTransport();
        var peers2 = new PeerCollection(transport2.Peer.Address);

        var peerExplorer2 = new PeerExplorer(transport2, peers2)
        {
            SeedPeers = [transport1.Peer]
        };
        using var gossip2 = new Gossip(transport2, peerExplorer2.Peers);

        await using var transports = new ServiceCollection
        {
            transport1,
            transport2,
        };

        transport1.MessageRouter.Register<HaveMessage>(_ =>
        {
            handled = true;
        });

        await transports.StartAsync(default);

        gossip2.PublishMessage(new PingMessage());

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

        var transport = TestUtils.CreateTransport();
        var peers = new PeerCollection(transport.Peer)
        {
            transport.Peer,
        };
        using var receiver = new Gossip(transport, peers);
        await using var sender1 = TestUtils.CreateTransport();
        sender1.MessageRouter.Register<IMessage>(HandelMessage);
        await using var sender2 = TestUtils.CreateTransport();
        sender2.MessageRouter.Register<IMessage>(HandelMessage);

        // await receiver.StartAsync(default);
        await transport.StartAsync(default);
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

    // private static Gossip CreateGossip(
    //     PrivateKey? privateKey = null,
    //     int? port = null,
    //     PeerCollection? peerExplorer = null)
    // {
    //     var transport = TestUtils.CreateTransport(privateKey, port);
    //     return new Gossip(transport, peerExplorer.Peers);
    // }

    // private static Gossip CreateGossip(
    //     ITransport transport,
    //     PeerExplorer peerExplorer)
    // {
    //     return new Gossip(transport, peerExplorer);
    // }
}
