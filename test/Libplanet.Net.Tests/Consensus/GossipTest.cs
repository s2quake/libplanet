using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Tests.Store;
using Libplanet.Net.Components;

namespace Libplanet.Net.Tests.Consensus;

public sealed class GossipTest
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task PublishMessage()
    {
        using var fx = new MemoryRepositoryFixture();
        var transport1 = TestUtils.CreateTransport();
        var transport2 = TestUtils.CreateTransport();
        var peers1 = new PeerCollection(transport1.Peer.Address)
        {
            transport2.Peer,
        };
        var peers2 = new PeerCollection(transport2.Peer.Address)
        {
            transport1.Peer,
        };
        await using var gossip1 = new Gossip(transport1, peers1);
        await using var gossip2 = new Gossip(transport2, peers2);
        await using var transports = new ServiceCollection
        {
            transport1,
            transport2,
        };

        await transports.StartAsync(default);

        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();
        transport1.MessageRouter.Register<ConsensusProposalMessage>(_ =>
        {
            tcs1.SetResult();
        });
        transport2.MessageRouter.Register<ConsensusProposalMessage>(_ =>
        {
            tcs2.SetResult();
        });

        var message = TestUtils.CreateConsensusPropose(fx.Block1, fx.Proposer, 1);
        gossip1.PublishMessage(message);

        await Task.WhenAll(tcs1.Task, tcs2.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(tcs1.Task.IsCompletedSuccessfully);
        Assert.True(tcs2.Task.IsCompletedSuccessfully);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddMessages()
    {
        using var fx = new MemoryRepositoryFixture();
        var received1 = 0;
        var received2 = 0;
        IMessage[] messages =
        [
            TestUtils.CreateConsensusPropose(fx.Block1, fx.Proposer, 1),
            TestUtils.CreateConsensusPropose(fx.Block2, fx.Proposer, 2),
            TestUtils.CreateConsensusPropose(fx.Block3, fx.Proposer, 3),
            TestUtils.CreateConsensusPropose(fx.Block4, fx.Proposer, 4),
        ];
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();
        var transport1 = TestUtils.CreateTransport();
        var transport2 = TestUtils.CreateTransport();
        var peers1 = new PeerCollection(transport1.Peer.Address)
        {
            transport2.Peer,
        };
        var peers2 = new PeerCollection(transport2.Peer.Address)
        {
            transport1.Peer,
        };
        await using var gossip1 = new Gossip(transport1, peers1);
        await using var gossip2 = new Gossip(transport2, peers2);
        await using var transports = new ServiceCollection
        {
            transport1,
            transport2,
        };
        transport1.MessageRouter.Register<ConsensusProposalMessage>(_ =>
        {
            if (Interlocked.Increment(ref received1) == messages.Length)
            {
                tcs1.SetResult();
            }
        });
        transport2.MessageRouter.Register<ConsensusProposalMessage>(_ =>
        {
            if (Interlocked.Increment(ref received2) == messages.Length)
            {
                tcs2.SetResult();
            }
        });

        await transports.StartAsync(default);
        

        Parallel.ForEach(messages, gossip1.PublishMessage);

        await Task.WhenAll(tcs1.Task, tcs2.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, received1);
        Assert.Equal(4, received2);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeerWithHaveMessage()
    {
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        await using var gossipA = new Gossip(transportA, peersA);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };

        await transports.StartAsync(default);

        TestUtils.InvokeDelay(() => transportB.Post(gossipA.Peer, new HaveMessage()), 100);
        await transportA.WaitAsync<HaveMessage>().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(transportB.Peer, gossipA.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotBroadcastToSeedPeers()
    {
        var tcs = new TaskCompletionSource();
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var peersB = new PeerCollection(transportB.Peer.Address);

        var peerExplorerB = new PeerExplorer(transportB, peersB)
        {
            SeedPeers = [transportA.Peer]
        };
        await using var gossipB = new Gossip(transportB, peerExplorerB.Peers);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };

        transportA.MessageRouter.Register<HaveMessage>(_ =>
        {
            tcs.SetResult();
        });

        await transports.StartAsync(default);

        gossipB.PublishMessage(new PingMessage());

        await Assert.ThrowsAsync<TimeoutException>(() => tcs.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotSendDuplicateMessageRequest()
    {
        var transport = TestUtils.CreateTransport();
        var peers = new PeerCollection(transport.Peer);
        await using var gossip = new Gossip(transport, peers);
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();

        await using var transports = new ServiceCollection
        {
            transport,
            transportA,
            transportB,
        };

        await transports.StartAsync(default);

        var handled = 0;
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();
        using var _1 = transportA.MessageRouter.Register<WantMessage>(_ =>
        {
            Interlocked.Increment(ref handled);
            tcs1.SetResult();
        });
        using var _2 = transportB.MessageRouter.Register<WantMessage>(_ =>
        {
            Interlocked.Increment(ref handled);
            tcs2.SetResult();
        });

        var message1 = new PingMessage();
        var message2 = new PongMessage();
        transportA.Post(gossip.Peer, new HaveMessage { Ids = [message1.Id, message2.Id] });
        transportB.Post(gossip.Peer, new HaveMessage { Ids = [message1.Id, message2.Id] });

        await Assert.ThrowsAsync<TimeoutException>(
            () => Task.WhenAll(tcs1.Task, tcs2.Task).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, handled);
    }
}
