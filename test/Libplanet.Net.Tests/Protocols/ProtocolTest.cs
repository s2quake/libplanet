using System.Diagnostics;
using Libplanet.Net.Components;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.TestUtilities;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Protocols;

public sealed class ProtocolTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task StartAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        var task = transportB.WaitAsync<PingMessage>(cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await transportA.PingAsync(transportB.Peer, cancellationToken));
        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        using var cancellationTokenSource = new CancellationTokenSource(500);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => transportA.PingAsync(transportB.Peer, cancellationTokenSource.Token));
        await task;
    }

    [Fact(Timeout = Timeout)]
    public async Task PingAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        var task = transportB.WaitAsync<PingMessage>((m, e) =>
        {
            if (e.Sender == transportA.Peer)
            {
                transportB.Post(e.Sender, new PongMessage(), e.Identity);
                return true;
            }

            return false;
        }, cancellationToken);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        var latency = await transportA.PingAsync(transportB.Peer, cancellationToken);
        var response = await task;
        Assert.IsType<PingMessage>(response.Message);
        Assert.Equal(transportA.Peer, response.Sender);
        Assert.True(latency > TimeSpan.Zero);
    }

    [Fact(Timeout = Timeout)]
    public async Task PingAsync_Twice()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        transportA.MessageRouter.Register(new PingMessageHandler(transportA));
        transportB.MessageRouter.Register(new PingMessageHandler(transportB));

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        var tasks = new List<Task<TimeSpan>>
        {
            transportA.PingAsync(transportB.Peer, cancellationToken),
            transportB.PingAsync(transportA.Peer, cancellationToken),
        };
        var latencies = await Task.WhenAll(tasks);
        Assert.All(latencies, latency => Assert.True(latency > TimeSpan.Zero));
    }

    [Fact(Timeout = Timeout)]
    public async Task PingAsync_ToClosedPeer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var peerA = transportA.Peer;
        var peerB = transportB.Peer;
        var peerC = transportC.Peer;

        var taskB = transportB.WaitPingAsync(peerA);
        var taskC = transportC.WaitPingAsync(peerA);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await transportC.StartAsync(cancellationToken);

        await transportA.PingAsync(peerB, cancellationToken);
        await transportA.PingAsync(peerC, cancellationToken);
        await Task.WhenAll(taskB, taskC);

        await transportC.StopAsync(cancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(() => transportA.PingAsync(peerC, cancellationToken));
        taskC = transportB.WaitPingAsync(peerA);
        await transportA.PingAsync(peerB, cancellationToken);

        await taskC;
        Assert.True(taskC.IsCompletedSuccessfully);
    }

    [Fact(Timeout = Timeout)]
    public async Task Bootstrap_Throw()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        var peersB = new PeerCollection(transportB.Peer.Address);

        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        await transportB.StartAsync(cancellationToken);
        await peerExplorerB.ExploreAsync([transportA.Peer], 3, cancellationToken);

        Assert.Empty(peerExplorerB.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);

        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB)
        {
            SeedPeers = [transportA.Peer],
        };
        var peerExplorerC = new PeerExplorer(transportC, peersC)
        {
            SeedPeers = [transportA.Peer],
        };

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };

        await transports.StartAsync(cancellationToken);

        await peerExplorerB.ExploreAsync(cancellationToken);
        await peerExplorerC.ExploreAsync(cancellationToken);

        Assert.Contains(transportC.Peer, peerExplorerB.Peers);
        Assert.Contains(transportB.Peer, peerExplorerC.Peers);

        peersA.Clear();
        peersB.Clear();
        peersC.Clear();

        await transportC.PingAsync(transportB.Peer, cancellationToken);
        await transportC.StopAsync(cancellationToken);

        await peerExplorerA.ExploreAsync([transportB.Peer], 3, cancellationToken);

        Assert.Contains(transportA.Peer, peerExplorerB.Peers);
        Assert.DoesNotContain(transportA.Peer, peerExplorerC.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task RemoveStalePeers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);

        using var peerExplorerA = new PeerExplorer(transportA, peersA);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };

        transportB.MessageRouter.Register(new PingMessageHandler(transportB));

        await transports.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(transportB.Peer, cancellationToken);
        Assert.Single(peerExplorerA.Peers);

        await transportB.StopAsync(cancellationToken);
        await Task.Delay(100, cancellationToken);
        await peerExplorerA.RefreshAsync(cancellationToken);
        Assert.Empty(peerExplorerA.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task RoutingTableFull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transport = CreateTransport();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var transportC = CreateTransport();

        var peers = new PeerCollection(transport.Peer.Address, bucketCount: 1, capacityPerBucket: 1);
        using var peerExplorer = new PeerExplorer(transport, peers);

        await using var transports = new ServiceCollection
        {
            transport,
            transportA,
            transportB,
            transportC,
        };

        await transports.StartAsync(cancellationToken);

        await transportA.PingAsync(transport.Peer, cancellationToken);
        await transportB.PingAsync(transport.Peer, cancellationToken);
        await transportC.PingAsync(transport.Peer, cancellationToken);

        Assert.Single(peerExplorer.Peers);
        Assert.Contains(transportA.Peer, peerExplorer.Peers);
        Assert.DoesNotContain(transportB.Peer, peerExplorer.Peers);
        Assert.DoesNotContain(transportC.Peer, peerExplorer.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task ReplacementCache()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transport = CreateTransport();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var transportC = CreateTransport();

        var peers = new PeerCollection(transport.Peer.Address, bucketCount: 1, capacityPerBucket: 1);
        using var peerExplorer = new PeerExplorer(transport, peers);
        await using var transports = new ServiceCollection
        {
            transport,
            transportA,
            transportB,
            transportC,
        };

        transportA.MessageRouter.Register(new PingMessageHandler(transportA));
        transportB.MessageRouter.Register(new PingMessageHandler(transportB));
        transportC.MessageRouter.Register(new PingMessageHandler(transportC));

        await transports.StartAsync(cancellationToken);

        await transportA.PingAsync(transport.Peer, cancellationToken);
        await transportB.PingAsync(transport.Peer, cancellationToken);
        await Task.Delay(100, cancellationToken);
        await transportC.PingAsync(transport.Peer, cancellationToken);

        Assert.Single(peerExplorer.Peers);
        Assert.Contains(transportA.Peer, peerExplorer.Peers);
        Assert.DoesNotContain(transportB.Peer, peerExplorer.Peers);
        Assert.DoesNotContain(transportC.Peer, peerExplorer.Peers);

        await transportA.StopAsync(cancellationToken);
        await peerExplorer.RefreshAsync(TimeSpan.Zero, cancellationToken);
        await peerExplorer.CheckReplacementCacheAsync(cancellationToken);

        Assert.Single(peerExplorer.Peers);
        Assert.DoesNotContain(transportA.Peer, peerExplorer.Peers);
        Assert.DoesNotContain(transportB.Peer, peerExplorer.Peers);
        Assert.Contains(transportC.Peer, peerExplorer.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task RemoveDeadReplacementCache()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transport = CreateTransport();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var transportC = CreateTransport();

        var peers = new PeerCollection(transport.Peer.Address, bucketCount: 1, capacityPerBucket: 1);
        using var peerExplorer = new PeerExplorer(transport, peers);

        await using var transports = new ServiceCollection
        {
            transport,
            transportA,
            transportB,
            transportC,
        };

        transportA.MessageRouter.Register(new PingMessageHandler(transportA));
        transportB.MessageRouter.Register(new PingMessageHandler(transportB));
        transportC.MessageRouter.Register(new PingMessageHandler(transportC));

        await transports.StartAsync(cancellationToken);

        await transportA.PingAsync(transport.Peer, cancellationToken);
        await transportB.PingAsync(transport.Peer, cancellationToken);

        Assert.Single(peerExplorer.Peers);
        Assert.Contains(transportA.Peer, peerExplorer.Peers);
        Assert.DoesNotContain(transportB.Peer, peerExplorer.Peers);

        await transportA.StopAsync(cancellationToken);
        await transportB.StopAsync(cancellationToken);

        await transportC.PingAsync(transport.Peer, cancellationToken);
        await peerExplorer.RefreshAsync(TimeSpan.Zero, cancellationToken);
        await peerExplorer.CheckReplacementCacheAsync(cancellationToken);

        Assert.Single(peerExplorer.Peers);
        Assert.DoesNotContain(transportA.Peer, peerExplorer.Peers);
        Assert.DoesNotContain(transportB.Peer, peerExplorer.Peers);
        Assert.Contains(transportC.Peer, peerExplorer.Peers);
    }

    [Theory(Timeout = 2 * Timeout)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task BroadcastMessage(int count)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var seed = CreateTransport();
        _ = new PeerExplorer(seed, new PeerCollection(seed.Peer.Address));
        await seed.StartAsync(cancellationToken);
        var transports = new ITransport[count];
        var peerExplorers = new PeerExplorer[count];
        for (var i = 0; i < count; i++)
        {
            transports[i] = CreateTransport();
            peerExplorers[i] = new PeerExplorer(transports[i], new PeerCollection(transports[i].Peer.Address))
            {
                SeedPeers = [seed.Peer],
            };
            await transports[i].StartAsync(cancellationToken);
        }
        await using var _1 = new AsyncDisposerCollection(transports);

        var taskList = new List<Task>();
        for (var i = 0; i < count; i++)
        {
            var task = transports[i].WaitAsync<TestMessage>(m => m.Data == "foo", cancellationToken);
            taskList.Add(task);
        }

        Trace.WriteLine("1");
        seed.Post([.. transports.Select(t => t.Peer)], new TestMessage { Data = "foo" });
        Trace.WriteLine("2");

        await Task.WhenAll(taskList);
        Trace.WriteLine("3");
        Assert.True(true);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotBroadcastToSourcePeer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        using var peerExplorerC = new PeerExplorer(transportC, peersC);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };

        await transports.StartAsync(cancellationToken);

        await transportB.PingAsync(transportA.Peer, cancellationToken);
        await transportC.PingAsync(transportB.Peer, cancellationToken);

        using var cancellationTokenSource = new CancellationTokenSource(10000);
        var taskA = transportA.WaitAsync<TestMessage>(m => m.Data == "foo", cancellationTokenSource.Token);
        var taskB = transportB.WaitAsync<TestMessage>(m => m.Data == "foo", cancellationToken);
        var taskC = transportC.WaitAsync<TestMessage>(m => m.Data == "foo", cancellationTokenSource.Token);
        peerExplorerA.Broadcast(new TestMessage { Data = "foo" });

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await taskA);
        await taskB;
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await taskC);
    }

    [Fact(Timeout = Timeout)]
    public async Task RefreshPeers()
    {
        const int peersCount = 10;
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var privateKeys = Enumerable.Range(0, peersCount).Select(
            i => GeneratePrivateKeyOfBucketIndex(signer.Address, i / 2));
        await using var transport = CreateTransport(signer);
        var peers = new PeerCollection(transport.Peer.Address);
        var transports = privateKeys.Select(key => CreateTransport(key)).ToArray();
        var peerses = transports.Select(t => new PeerCollection(t.Peer.Address)).ToArray();
        var peerExploreres = transports.Select(t => new PeerExplorer(t, peerses[Array.IndexOf(transports, t)])).ToArray();
        var peerExplorer = new PeerExplorer(transport, peers);
        await using var _1 = new AsyncDisposerCollection(transports);
        using var _2 = new DisposerCollection(peerExploreres);

        await transport.StartAsync(cancellationToken);

        for (var i = 0; i < transports.Length; i++)
        {
            await transports[i].StartAsync(cancellationToken);
        }

        for (var i = 0; i < transports.Length; i++)
        {
            var peerState = new PeerState
            {
                Peer = transports[i].Peer,
                LastUpdated = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2),
            };
            var peer = transports[i].Peer;
            var lastUpdated = peerState.LastUpdated;
            var latency = TimeSpan.Zero;
            Assert.True(peerExplorer.AddOrUpdate(peer, lastUpdated, latency));
        }

        var stalePeers = peerExplorer.Peers.GetStalePeers(TimeSpan.FromMinutes(1));
        Assert.Equal(peersCount, peerExplorer.Peers.Count);
        Assert.Equal(peersCount / 2, stalePeers.Length);
        Assert.Equal(peersCount / 2, peerExplorer.Peers.Buckets.Count(item => !item.IsEmpty));

        await peerExplorer.RefreshAsync(TimeSpan.FromMinutes(1), cancellationToken);
        Assert.NotEqual(
            stalePeers,
            peerExplorer.Peers.GetStalePeers(TimeSpan.FromMinutes(1)));
        Assert.Equal(
            peersCount / 2,
            peerExplorer.Peers.GetStalePeers(TimeSpan.FromMinutes(1)).Length);
        Assert.Equal(peersCount / 2, peerExplorer.Peers.Buckets.Count(item => !item.IsEmpty));

        await peerExplorer.RefreshAsync(TimeSpan.FromMinutes(1), cancellationToken);
        Assert.Empty(peerExplorer.Peers.GetStalePeers(TimeSpan.FromMinutes(1)));
    }
}
