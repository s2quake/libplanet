using Libplanet.Extensions;
using Libplanet.Net.Components;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class GossipTest(ITestOutputHelper output)
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task PublishMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var block = new BlockBuilder
        {
        }.Create(proposer);
        var transport1 = CreateTransport();
        var transport2 = CreateTransport();
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
        var waitTask1 = transport1.WaitAsync<ConsensusProposalMessage>(cancellationToken);
        var waitTask2 = transport2.WaitAsync<ConsensusProposalMessage>(cancellationToken);

        await transports.StartAsync(cancellationToken);

        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(proposer);
        var proposalMessage = new ConsensusProposalMessage { Proposal = proposal };

        gossip1.Broadcast(proposalMessage);

        var reply1 = await waitTask1.WaitAsync(cancellationToken);
        var reply2 = await waitTask2.WaitAsync(cancellationToken);

        Assert.Equal(proposalMessage, reply1.Message);
        Assert.Equal(proposalMessage, reply2.Message);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task AddMessages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);
        var (block2, _) = blockchain.ProposeAndAppend(proposer);
        var (block3, _) = blockchain.ProposeAndAppend(proposer);
        var (block4, _) = blockchain.ProposeAndAppend(proposer);
        ConsensusProposalMessage[] messages =
        [
            new () { Proposal = new ProposalBuilder { Block = block1, }.Create(proposer) },
            new () { Proposal = new ProposalBuilder { Block = block2, }.Create(proposer) },
            new () { Proposal = new ProposalBuilder { Block = block3, }.Create(proposer) },
            new () { Proposal = new ProposalBuilder { Block = block4, }.Create(proposer) },
        ];
        var transport1 = CreateTransport();
        var transport2 = CreateTransport();
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

        var counter1 = transport1.Counter<ConsensusProposalMessage>();
        var counter2 = transport2.Counter<ConsensusProposalMessage>();
        var waitTask1 = counter1.CountChanged.WaitAsync(e => e == 4);
        var waitTask2 = counter2.CountChanged.WaitAsync(e => e == 4);

        await transports.StartAsync(cancellationToken);

        Parallel.ForEach(messages, gossip1.Broadcast);

        var count1 = await waitTask1.WaitAsync(cancellationToken);
        var count2 = await waitTask2.WaitAsync(cancellationToken);

        Assert.Equal(4, count1);
        Assert.Equal(4, count2);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task AddPeerWithHaveMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        await using var gossipA = new Gossip(transportA, peersA);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };

        await transports.StartAsync(cancellationToken);

        var waitTaskA = transportA.WaitAsync<HaveMessage>(cancellationToken);
        transportB.Post(gossipA.Peer, new HaveMessage());
        await waitTaskA.WaitAsync(WaitTimeout2, cancellationToken);

        Assert.Contains(transportB.Peer, gossipA.Peers);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task DoNotBroadcastToSeedPeers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
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
        var waitTask = transportA.WaitAsync<HaveMessage>(cancellationToken);

        await transports.StartAsync(cancellationToken);

        gossipB.Broadcast(new PingMessage());

        await Assert.ThrowsAsync<TimeoutException>(() => waitTask.WaitAsync(WaitTimeout2, cancellationToken));
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task DoNotSendDuplicateMessageRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transport = CreateTransport();
        var peers = new PeerCollection(transport.Peer);
        await using var gossip = new Gossip(transport, peers);
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        await using var transports = new ServiceCollection
        {
            transport,
            transportA,
            transportB,
        };

        await transports.StartAsync(cancellationToken);

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
            () => Task.WhenAll(tcs1.Task, tcs2.Task).WaitAsync(WaitTimeout2, cancellationToken));
        Assert.Equal(1, handled);
    }
}
