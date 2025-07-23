using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Net.NetMQ;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Protocols;

public sealed class ProtocolTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task Start()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        var task = transportB.WaitMessageAsync<PingMessage>(default);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await transportA.PingAsync(transportB.Peer, default));
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        using var cancellationTokenSource = new CancellationTokenSource(500);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => transportA.PingAsync(transportB.Peer, cancellationTokenSource.Token));
        await task;
    }

    [Fact(Timeout = Timeout)]
    public async Task Ping()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        var task = transportB.WaitMessageAsync<PingMessage>((m, e) =>
        {
            if (e.Sender == transportA.Peer)
            {
                transportB.Post(e.Sender, new PongMessage(), e.Identity);
                return true;
            }

            return false;
        }, default);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        var latency = await transportA.PingAsync(transportB.Peer, default);
        var response = await task;
        Assert.IsType<PingMessage>(response.Message);
        Assert.Equal(transportA.Peer, response.Sender);
        Assert.True(latency > TimeSpan.Zero);
    }

    [Fact(Timeout = Timeout)]
    public async Task PingTwice()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        transportA.MessageHandlers.Add(new PingMessageHandler(transportA));
        transportB.MessageHandlers.Add(new PingMessageHandler(transportB));

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var tasks = new List<Task<TimeSpan>>
        {
            transportA.PingAsync(transportB.Peer, default),
            transportB.PingAsync(transportA.Peer, default),
        };
        var latencies = await Task.WhenAll(tasks);
        Assert.All(latencies, latency => Assert.True(latency > TimeSpan.Zero));
    }

    [Fact(Timeout = Timeout)]
    public async Task PingToClosedPeer()
    {
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var transportC = TestUtils.CreateTransport();
        var peerA = transportA.Peer;
        var peerB = transportB.Peer;
        var peerC = transportC.Peer;

        var taskB = transportB.WaitPingAsync(peerA);
        var taskC = transportC.WaitPingAsync(peerA);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await transportA.PingAsync(peerB, default);
        await transportA.PingAsync(peerC, default);
        await Task.WhenAll(taskB, taskC);

        await transportC.StopAsync(default);
        await Assert.ThrowsAsync<TimeoutException>(async () => await transportA.PingAsync(peerC, default));
        taskC = transportB.WaitPingAsync(peerA);
        await transportA.PingAsync(peerB, default);

        await taskC;
        Assert.True(taskC.IsCompletedSuccessfully);
    }

    [Fact(Timeout = Timeout)]
    public async Task Bootstrap_Throw()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();

        var peerDiscoveryB = new PeerService(transportB);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await peerDiscoveryB.BootstrapAsync([transportA.Peer], 3, default));
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapAsync()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();
        var peerDiscoveryA = new PeerService(transportA);
        var peerDiscoveryB = new PeerService(transportB);
        var peerDiscoveryC = new PeerService(transportC);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await peerDiscoveryB.BootstrapAsync([transportA.Peer], 3, default);
        await peerDiscoveryC.BootstrapAsync([transportA.Peer], 3, default);

        Assert.True(peerDiscoveryB.Contains(transportC.Peer));
        Assert.True(peerDiscoveryC.Contains(transportB.Peer));

        peerDiscoveryA.Clear();
        peerDiscoveryB.Clear();
        peerDiscoveryC.Clear();

        await transportB.PingAsync(transportC.Peer, default);
        await transportC.StopAsync(default);
        await peerDiscoveryA.BootstrapAsync([transportB.Peer], 3, default);

        Assert.True(peerDiscoveryB.Contains(transportA.Peer));
        Assert.False(peerDiscoveryC.Contains(transportA.Peer));
    }

    [Fact(Timeout = Timeout)]
    public async Task RemoveStalePeers()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();

        var peerDiscoveryA = new PeerService(transportA);
        transportB.MessageHandlers.Add(new PingMessageHandler(transportB));

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await peerDiscoveryA.RefreshPeerAsync(transportB.Peer, default);
        Assert.Single(peerDiscoveryA.Peers);

        await transportB.StopAsync(default);
        await Task.Delay(100);
        await peerDiscoveryA.RefreshPeersAsync(TimeSpan.Zero, default);
        Assert.Empty(peerDiscoveryA.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task RoutingTableFull()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();

        var peerSerivceOptions = new PeerServiceOptions
        {
            BucketCount = 1,
            CapacityPerBucket = 1,
        };

        await using var peerService = new PeerService(transport, peerSerivceOptions);

        await transport.StartAsync(default);
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await transportA.PingAsync(transport.Peer, default);
        await transportB.PingAsync(transport.Peer, default);
        await transportC.PingAsync(transport.Peer, default);

        Assert.Single(peerService.Peers);
        Assert.Contains(transportA.Peer, peerService.Peers);
        Assert.DoesNotContain(transportB.Peer, peerService.Peers);
        Assert.DoesNotContain(transportC.Peer, peerService.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task ReplacementCache()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();

        var peerServiceOptions = new PeerServiceOptions
        {
            BucketCount = 1,
            CapacityPerBucket = 1,
        };
        var peerService = new PeerService(transport, peerServiceOptions);
        transportA.MessageHandlers.Add(new PingMessageHandler(transportA));
        transportB.MessageHandlers.Add(new PingMessageHandler(transportB));
        transportC.MessageHandlers.Add(new PingMessageHandler(transportC));

        await transport.StartAsync(default);
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await transportA.PingAsync(transport.Peer, default);
        await transportB.PingAsync(transport.Peer, default);
        await Task.Delay(100);
        await transportC.PingAsync(transport.Peer, default);

        Assert.Single(peerService.Peers);
        Assert.Contains(transportA.Peer, peerService.Peers);
        Assert.DoesNotContain(transportB.Peer, peerService.Peers);
        Assert.DoesNotContain(transportC.Peer, peerService.Peers);

        await transportA.StopAsync(default);
        await peerService.RefreshPeersAsync(TimeSpan.Zero, default);
        await peerService.CheckReplacementCacheAsync(default);

        Assert.Single(peerService.Peers);
        Assert.DoesNotContain(transportA.Peer, peerService.Peers);
        Assert.DoesNotContain(transportB.Peer, peerService.Peers);
        Assert.Contains(transportC.Peer, peerService.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task RemoveDeadReplacementCache()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();

        var peerServiceOptions = new PeerServiceOptions
        {
            BucketCount = 1,
            CapacityPerBucket = 1,
        };
        var peerService = new PeerService(transport, peerServiceOptions);
        transportA.MessageHandlers.Add(new PingMessageHandler(transportA));
        transportB.MessageHandlers.Add(new PingMessageHandler(transportB));
        transportC.MessageHandlers.Add(new PingMessageHandler(transportC));

        await transport.StartAsync(default);
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await transportA.PingAsync(transport.Peer, default);
        await transportB.PingAsync(transport.Peer, default);

        Assert.Single(peerService.Peers);
        Assert.Contains(transportA.Peer, peerService.Peers);
        Assert.DoesNotContain(transportB.Peer, peerService.Peers);

        await transportA.StopAsync(default);
        await transportB.StopAsync(default);

        await transportC.PingAsync(transport.Peer, default);
        await peerService.RefreshPeersAsync(TimeSpan.Zero, default);
        await peerService.CheckReplacementCacheAsync(default);

        Assert.Single(peerService.Peers);
        Assert.DoesNotContain(transportA.Peer, peerService.Peers);
        Assert.DoesNotContain(transportB.Peer, peerService.Peers);
        Assert.Contains(transportC.Peer, peerService.Peers);
    }

    [Theory(Timeout = 2 * Timeout)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task BroadcastMessage(int count)
    {
        await using var seed = TestUtils.CreateTransport();
        _ = new PeerService(seed);
        await seed.StartAsync(default);
        var transports = new NetMQTransport[count];
        var peerServices = new PeerService[count];
        for (var i = 0; i < count; i++)
        {
            transports[i] = TestUtils.CreateTransport();
            peerServices[i] = new PeerService(transports[i]);
            await transports[i].StartAsync(default);
        }
        await using var _1 = new AsyncDisposerCollection(transports);

        for (var i = 0; i < count; i++)
        {
            await peerServices[i].BootstrapAsync([seed.Peer], 3, default);
        }

        var taskList = new List<Task>();
        for (var i = 0; i < count; i++)
        {
            var task = transports[i].WaitMessageAsync<TestMessage>(m => m.Data == "foo", default);
            taskList.Add(task);
        }

        Trace.WriteLine("1");
        seed.Post([.. transports.Select(t => t.Peer)], new TestMessage { Data = "foo" });
        Trace.WriteLine("2");

        await Task.WhenAll(taskList);
        Trace.WriteLine("3");
    }

    // [Fact(Timeout = Timeout)]
    // public async Task BroadcastGuarantee()
    // {
    //     // Make sure t1 and t2 is in same bucket of seed's routing table.
    //     var privateKey0 = new PrivateKey(
    //     [
    //         0x1a, 0x55, 0x30, 0x84, 0xe8, 0x9e, 0xee, 0x1e, 0x9f, 0xe2, 0xd1, 0x49, 0xe7, 0xa9,
    //         0x53, 0xa9, 0xb4, 0xe4, 0xfe, 0x5a, 0xc1, 0x6c, 0x61, 0x9f, 0x54, 0x8f, 0x5e, 0xd9,
    //         0x7f, 0xa3, 0xa0, 0x79,
    //     ]);
    //     var privateKey1 = new PrivateKey(
    //     [
    //         0x8e, 0x26, 0x31, 0x4a, 0xee, 0x84, 0xd, 0x8a, 0xea, 0x7b, 0x6, 0xf8, 0x81, 0x5f,
    //         0x69, 0xb3, 0x44, 0x46, 0xe0, 0x27, 0x65, 0x17, 0x1, 0x16, 0x58, 0x26, 0x69, 0x93,
    //         0x48, 0xbb, 0xf, 0xb4,
    //     ]);
    //     var privateKey2 = new PrivateKey(
    //     [
    //         0xd4, 0x6b, 0x4b, 0x38, 0xde, 0x39, 0x25, 0x3b, 0xd8, 0x1, 0x9d, 0x2, 0x2, 0x7a,
    //         0x90, 0x9, 0x46, 0x2f, 0xc1, 0xd3, 0xd9, 0xa, 0xa6, 0xf4, 0xfa, 0x9a, 0x6, 0xa3,
    //         0x60, 0xed, 0xf3, 0xd7,
    //     ]);

    //     await using var seed = TestUtils.CreateTransport(privateKey0);
    //     await using var t1 = TestUtils.CreateTransport(privateKey1, true);
    //     await using var t2 = TestUtils.CreateTransport(privateKey2);
    //     var seedTable = new RoutingTable(seed.Peer.Address);
    //     _ = new PeerService(seedTable, seed);
    //     var t1Table = new RoutingTable(t1.Peer.Address);
    //     var t2Table = new RoutingTable(t2.Peer.Address);
    //     var peerDiscovery1 = new PeerService(t1Table, t1);
    //     var peerDiscovery2 = new PeerService(t2Table, t2);

    //     await seed.StartAsync(default);
    //     await t1.StartAsync(default);
    //     await t2.StartAsync(default);

    //     await peerDiscovery1.BootstrapAsync([seed.Peer], 3, default);
    //     await peerDiscovery2.BootstrapAsync([seed.Peer], 3, default);

    //     var tcs = new CancellationTokenSource();
    //     var task = t2.WaitForTestMessageWithData("foo", tcs.Token);

    //     seed.BroadcastTestMessage(default, "foo");

    //     tcs.CancelAfter(TimeSpan.FromSeconds(5));
    //     await task;

    //     Assert.True(t2.ReceivedTestMessageOfData("foo"));

    //     var tcs1 = new CancellationTokenSource();
    //     task = t2.WaitForTestMessageWithData("bar", tcs.Token);

    //     seed.BroadcastTestMessage(default, "bar");

    //     tcs.CancelAfter(TimeSpan.FromSeconds(5));
    //     await task;

    //     Assert.True(t2.ReceivedTestMessageOfData("bar"));

    //     tcs = new CancellationTokenSource();
    //     task = t2.WaitForTestMessageWithData("baz", tcs.Token);

    //     seed.BroadcastTestMessage(default, "baz");

    //     tcs.CancelAfter(TimeSpan.FromSeconds(5));
    //     await task;

    //     Assert.True(t2.ReceivedTestMessageOfData("baz"));

    //     tcs = new CancellationTokenSource();
    //     task = t2.WaitForTestMessageWithData("qux", tcs.Token);

    //     seed.BroadcastTestMessage(default, "qux");

    //     tcs.CancelAfter(TimeSpan.FromSeconds(5));
    //     await task;

    //     Assert.True(t2.ReceivedTestMessageOfData("qux"));
    // }

    [Fact(Timeout = Timeout)]
    public async Task DoNotBroadcastToSourcePeer()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();
        await using var peerServiceA = new PeerService(transportA);
        await using var peerServiceB = new PeerService(transportB);
        await using var peerServiceC = new PeerService(transportC);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await transportB.PingAsync(transportA.Peer, default);
        await transportC.PingAsync(transportB.Peer, default);

        using var cancellationTokenSource = new CancellationTokenSource(10000);
        var taskA = transportA.WaitMessageAsync<TestMessage>(m => m.Data == "foo", cancellationTokenSource.Token);
        var taskB = transportB.WaitMessageAsync<TestMessage>(m => m.Data == "foo", default);
        var taskC = transportC.WaitMessageAsync<TestMessage>(m => m.Data == "foo", cancellationTokenSource.Token);
        var peers = peerServiceA.PeersToBroadcast(default);
        transportA.Post(peers, new TestMessage { Data = "foo" });

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await taskA);
        await taskB;
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await taskC);
    }

    [Fact(Timeout = Timeout)]
    public async Task RefreshTable()
    {
        const int peersCount = 10;
        var privateKey = new PrivateKey();
        var privateKeys = Enumerable.Range(0, peersCount).Select(
            i => TestUtils.GeneratePrivateKeyOfBucketIndex(privateKey.Address, i / 2));
        await using var transport = TestUtils.CreateTransport(privateKey);
        var transports = privateKeys.Select(key => TestUtils.CreateTransport(key)).ToArray();
        var peerServices = transports.Select(t => new PeerService(t)).ToArray();
        var peerService = new PeerService(transport);
        await using var _1 = new AsyncDisposerCollection(transports);
        await using var _2 = new AsyncDisposerCollection(peerServices);

        await transport.StartAsync(default);

        for (var i = 0; i < transports.Length; i++)
        {
            await transports[i].StartAsync(default);
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
            Assert.True(peerService.AddOrUpdate(peer, lastUpdated));
        }

        var stalePeers = peerService.GetStalePeers(TimeSpan.FromMinutes(1));
        Assert.Equal(peersCount, peerService.Peers.Count());
        Assert.Equal(peersCount / 2, stalePeers.Length);
        Assert.Equal(peersCount / 2, peerService.Peers.Buckets.Count(item => !item.IsEmpty));

        await peerService.RefreshPeersAsync(TimeSpan.FromMinutes(1), default);
        Assert.NotEqual(
            stalePeers,
            peerService.GetStalePeers(TimeSpan.FromMinutes(1)));
        Assert.Equal(
            peersCount / 2,
            peerService.GetStalePeers(TimeSpan.FromMinutes(1)).Length);
        Assert.Equal(peersCount / 2, peerService.Peers.Buckets.Count(item => !item.IsEmpty));

        await peerService.RefreshPeersAsync(TimeSpan.FromMinutes(1), default);
        Assert.Empty(peerService.GetStalePeers(TimeSpan.FromMinutes(1)));
    }
}
