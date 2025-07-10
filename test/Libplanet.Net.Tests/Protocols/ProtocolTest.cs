using System.Diagnostics;
using System.Threading.Tasks;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Protocols;

public sealed class ProtocolTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;
    private readonly Dictionary<Address, NetMQTransport> _transports = [];

    [Fact(Timeout = Timeout)]
    public async Task Start()
    {
        var transportOptionsA = new TransportOptions
        {
            SendTimeout = TimeSpan.FromMilliseconds(500),
            ReceiveTimeout = TimeSpan.FromMilliseconds(500),
        };
        await using var transportA = TestUtils.CreateTransport(options: transportOptionsA);
        await using var transportB = TestUtils.CreateTransport();
        var task = transportB.Process.WaitAsync(m => m.Message is PingMessage, default);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await transportA.PingAsync(transportB.Peer, default));
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await Assert.ThrowsAsync<TimeoutException>(() => transportA.PingAsync(transportB.Peer, default));
        await task;
    }

    [Fact(Timeout = Timeout)]
    public async Task Ping()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        var task = transportB.Process.WaitAsync(m =>
        {
            if (m.Message is PingMessage && m.Sender == transportA.Peer)
            {
                transportB.Reply(m.Identity, new PongMessage());
                return true;
            }

            return false;
        }, default);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportA.PingAsync(transportB.Peer, default);
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact(Timeout = Timeout)]
    public async Task PingTwice()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        var taskA = transportA.WaitPingAsync(transportB.Peer);
        var taskB = transportB.WaitPingAsync(transportA.Peer);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await transportA.PingAsync(transportB.Peer, default);
        await taskB;
        await transportB.PingAsync(transportA.Peer, default);
        await taskA;

        Assert.True(taskA.IsCompletedSuccessfully);
        Assert.True(taskB.IsCompletedSuccessfully);
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

        var tableB = new RoutingTable(transportB.Peer.Address);
        var peerDiscoveryB = new PeerDiscovery(tableB, transportB);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await peerDiscoveryB.BootstrapAsync([transportA.Peer], 3, default));
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapAsync()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();
        var tableA = new RoutingTable(transportA.Peer.Address);
        var tableB = new RoutingTable(transportB.Peer.Address);
        var tableC = new RoutingTable(transportC.Peer.Address);
        var peerDiscoveryA = new PeerDiscovery(tableA, transportA);
        var peerDiscoveryB = new PeerDiscovery(tableB, transportB);
        var peerDiscoveryC = new PeerDiscovery(tableC, transportC);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await peerDiscoveryB.BootstrapAsync([transportA.Peer], 3, default);
        await peerDiscoveryC.BootstrapAsync([transportA.Peer], 3, default);

        Assert.True(tableB.Contains(transportC.Peer));
        Assert.True(tableC.Contains(transportB.Peer));

        tableA.Clear();
        tableB.Clear();
        tableC.Clear();

        await transportB.PingAsync(transportC.Peer, default);
        await transportC.StopAsync(default);
        await peerDiscoveryA.BootstrapAsync([transportB.Peer], 3, default);

        Assert.True(tableB.Contains(transportA.Peer));
        Assert.False(tableC.Contains(transportA.Peer));
    }

    [Fact(Timeout = Timeout)]
    public async Task RemoveStalePeers()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();

        var tableA = new RoutingTable(transportA.Peer.Address);
        var peerDiscoveryA = new PeerDiscovery(tableA, transportA);
        using var _ = transportB.RegisterPingHandler();

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        await peerDiscoveryA.RefreshPeerAsync(transportB.Peer, default);
        Assert.Single(tableA);

        await transportB.StopAsync(default);
        await Task.Delay(100);
        await peerDiscoveryA.RefreshPeersAsync(TimeSpan.Zero, default);
        Assert.Empty(tableA);
    }

    [Fact(Timeout = Timeout)]
    public async Task RoutingTableFull()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();

        var table = new RoutingTable(transport.Peer.Address, bucketCount: 1, capacityPerBucket: 1);
        var _ = new PeerDiscovery(table, transport);

        await transport.StartAsync(default);
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await transportA.PingAsync(transport.Peer, default);
        await transportB.PingAsync(transport.Peer, default);
        await transportC.PingAsync(transport.Peer, default);

        Assert.Single(table.Peers);
        Assert.Contains(transportA.Peer, table.Peers);
        Assert.DoesNotContain(transportB.Peer, table.Peers);
        Assert.DoesNotContain(transportC.Peer, table.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task ReplacementCache()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();

        var table = new RoutingTable(transport.Peer.Address, bucketCount: 1, capacityPerBucket: 1);
        var peerDiscovery = new PeerDiscovery(table, transport);
        using var _1 = transportA.RegisterPingHandler();
        using var _2 = transportB.RegisterPingHandler();
        using var _3 = transportC.RegisterPingHandler();

        await transport.StartAsync(default);
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await transportA.PingAsync(transport.Peer, default);
        await transportB.PingAsync(transport.Peer, default);
        await Task.Delay(100);
        await transportC.PingAsync(transport.Peer, default);

        Assert.Single(table.Peers);
        Assert.Contains(transportA.Peer, table.Peers);
        Assert.DoesNotContain(transportB.Peer, table.Peers);
        Assert.DoesNotContain(transportC.Peer, table.Peers);

        await transportA.StopAsync(default);
        await peerDiscovery.RefreshPeersAsync(TimeSpan.Zero, default);
        await peerDiscovery.CheckReplacementCacheAsync(default);

        Assert.Single(table.Peers);
        Assert.DoesNotContain(transportA.Peer, table.Peers);
        Assert.DoesNotContain(transportB.Peer, table.Peers);
        Assert.Contains(transportC.Peer, table.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task RemoveDeadReplacementCache()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();

        var table = new RoutingTable(transport.Peer.Address, bucketCount: 1, capacityPerBucket: 1);
        var peerDiscovery = new PeerDiscovery(table, transport);
        using var _1 = transportA.RegisterPingHandler();
        using var _2 = transportB.RegisterPingHandler();
        using var _3 = transportC.RegisterPingHandler();

        await transport.StartAsync(default);
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportC.StartAsync(default);

        await transportA.PingAsync(transport.Peer, default);
        await transportB.PingAsync(transport.Peer, default);

        Assert.Single(table.Peers);
        Assert.Contains(transportA.Peer, table.Peers);
        Assert.DoesNotContain(transportB.Peer, table.Peers);

        await transportA.StopAsync(default);
        await transportB.StopAsync(default);

        await transportC.PingAsync(transport.Peer, default);
        await peerDiscovery.RefreshPeersAsync(TimeSpan.Zero, default);
        await peerDiscovery.CheckReplacementCacheAsync(default);

        Assert.Single(table.Peers);
        Assert.DoesNotContain(transportA.Peer, table.Peers);
        Assert.DoesNotContain(transportB.Peer, table.Peers);
        Assert.Contains(transportC.Peer, table.Peers);
    }

    [Theory(Timeout = 2 * Timeout)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(50)]
    public async Task BroadcastMessage(int count)
    {
        await using var seed = TestUtils.CreateTransport();
        var seedTable = new RoutingTable(seed.Peer.Address);
        _ = new PeerDiscovery(seedTable, seed);
        await seed.StartAsync(default);
        var transports = new NetMQTransport[count];
        var tables = new RoutingTable[count];
        var peerDiscoveries = new PeerDiscovery[count];
        for (var i = 0; i < count; i++)
        {
            transports[i] = TestUtils.CreateTransport();
            tables[i] = new RoutingTable(transports[i].Peer.Address);
            peerDiscoveries[i] = new PeerDiscovery(tables[i], transports[i]);
            await transports[i].StartAsync(default);
        }
        await using var _1 = new AsyncDisposerCollection(transports);

        for (var i = 0; i < count; i++)
        {
            await peerDiscoveries[i].BootstrapAsync([seed.Peer], 3, default);
        }

        var taskList = new List<Task>();
        for (var i = 0; i < count; i++)
        {
            var task = transports[i].WaitMessageAsync<TestMessage>(m => m.Data == "foo", default);
            taskList.Add(task);
        }

        Trace.WriteLine("1");
        seed.Broadcast([.. transports.Select(t => t.Peer)], new TestMessage { Data = "foo" });
        Trace.WriteLine("2");

        await Task.WhenAll(taskList);
        Trace.WriteLine("3");
    }

    // [Fact(Timeout = Timeout)]
    // public async Task BroadcastGuarantee()
    // {
    //     // Make sure t1 and t2 is in same bucket of seed's routing table.
    //     var privateKey0 = new PrivateKey(new byte[]
    //     {
    //         0x1a, 0x55, 0x30, 0x84, 0xe8, 0x9e, 0xee, 0x1e, 0x9f, 0xe2, 0xd1, 0x49, 0xe7, 0xa9,
    //         0x53, 0xa9, 0xb4, 0xe4, 0xfe, 0x5a, 0xc1, 0x6c, 0x61, 0x9f, 0x54, 0x8f, 0x5e, 0xd9,
    //         0x7f, 0xa3, 0xa0, 0x79,
    //     });
    //     var privateKey1 = new PrivateKey(new byte[]
    //     {
    //         0x8e, 0x26, 0x31, 0x4a, 0xee, 0x84, 0xd, 0x8a, 0xea, 0x7b, 0x6, 0xf8, 0x81, 0x5f,
    //         0x69, 0xb3, 0x44, 0x46, 0xe0, 0x27, 0x65, 0x17, 0x1, 0x16, 0x58, 0x26, 0x69, 0x93,
    //         0x48, 0xbb, 0xf, 0xb4,
    //     });
    //     var privateKey2 = new PrivateKey(new byte[]
    //     {
    //         0xd4, 0x6b, 0x4b, 0x38, 0xde, 0x39, 0x25, 0x3b, 0xd8, 0x1, 0x9d, 0x2, 0x2, 0x7a,
    //         0x90, 0x9, 0x46, 0x2f, 0xc1, 0xd3, 0xd9, 0xa, 0xa6, 0xf4, 0xfa, 0x9a, 0x6, 0xa3,
    //         0x60, 0xed, 0xf3, 0xd7,
    //     });

    //     var seed = CreateNetMQTransport(privateKey0);
    //     var t1 = CreateNetMQTransport(privateKey1, true);
    //     var t2 = CreateNetMQTransport(privateKey2);
    //     await StartNetMQTransportAsync(seed);
    //     await StartNetMQTransportAsync(t1);
    //     await StartNetMQTransportAsync(t2);

    //     try
    //     {
    //         await t1.BootstrapAsync(new[] { seed.Peer });
    //         await t2.BootstrapAsync(new[] { seed.Peer });

    //         Log.Debug("Bootstrap completed");

    //         var tcs = new CancellationTokenSource();
    //         var task = t2.WaitForTestMessageWithData("foo", tcs.Token);

    //         seed.BroadcastTestMessage(default, "foo");
    //         Log.Debug("Broadcast \"foo\" completed");

    //         tcs.CancelAfter(TimeSpan.FromSeconds(5));
    //         await task;

    //         Assert.True(t2.ReceivedTestMessageOfData("foo"));

    //         tcs = new CancellationTokenSource();
    //         task = t2.WaitForTestMessageWithData("bar", tcs.Token);

    //         seed.BroadcastTestMessage(default, "bar");
    //         Log.Debug("Broadcast \"bar\" completed");

    //         tcs.CancelAfter(TimeSpan.FromSeconds(5));
    //         await task;

    //         Assert.True(t2.ReceivedTestMessageOfData("bar"));

    //         tcs = new CancellationTokenSource();
    //         task = t2.WaitForTestMessageWithData("baz", tcs.Token);

    //         seed.BroadcastTestMessage(default, "baz");
    //         Log.Debug("Broadcast \"baz\" completed");

    //         tcs.CancelAfter(TimeSpan.FromSeconds(5));
    //         await task;

    //         Assert.True(t2.ReceivedTestMessageOfData("baz"));

    //         tcs = new CancellationTokenSource();
    //         task = t2.WaitForTestMessageWithData("qux", tcs.Token);

    //         seed.BroadcastTestMessage(default, "qux");
    //         Log.Debug("Broadcast \"qux\" completed");

    //         tcs.CancelAfter(TimeSpan.FromSeconds(5));
    //         await task;

    //         Assert.True(t2.ReceivedTestMessageOfData("qux"));
    //     }
    //     finally
    //     {
    //         seed.Dispose();
    //         t1.Dispose();
    //         t2.Dispose();
    //     }
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task DoNotBroadcastToSourcePeer()
    // {
    //     NetMQTransport transportA = CreateNetMQTransport(new PrivateKey());
    //     NetMQTransport transportB = CreateNetMQTransport(new PrivateKey());
    //     NetMQTransport transportC = CreateNetMQTransport(new PrivateKey());

    //     await StartNetMQTransportAsync(transportA);
    //     await StartNetMQTransportAsync(transportB);
    //     await StartNetMQTransportAsync(transportC);

    //     try
    //     {
    //         await transportA.AddPeersAsync(new[] { transportB.Peer }, null);
    //         await transportB.AddPeersAsync(new[] { transportC.Peer }, null);

    //         transportA.BroadcastTestMessage(default, "foo");
    //         await transportC.WaitForTestMessageWithData("foo");
    //         await Task.Delay(100);

    //         Assert.True(transportC.ReceivedTestMessageOfData("foo"));
    //         Assert.False(transportA.ReceivedTestMessageOfData("foo"));
    //     }
    //     finally
    //     {
    //         transportA.Dispose();
    //         transportB.Dispose();
    //         transportC.Dispose();
    //     }
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task RefreshTable()
    // {
    //     const int peersCount = 10;
    //     var privateKey = new PrivateKey();
    //     var privateKeys = Enumerable.Range(0, peersCount).Select(
    //         i => GeneratePrivateKeyOfBucketIndex(privateKey.Address, i / 2));
    //     NetMQTransport transport = CreateNetMQTransport(privateKey);
    //     NetMQTransport[] transports =
    //         privateKeys.Select(key => CreateNetMQTransport(key)).ToArray();

    //     await StartNetMQTransportAsync(transport);
    //     foreach (var t in transports)
    //     {
    //         await StartNetMQTransportAsync(t);
    //     }

    //     try
    //     {
    //         foreach (var t in transports)
    //         {
    //             transport.Table.AddPeer(
    //                 t.Peer,
    //                 DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2));
    //         }

    //         IReadOnlyList<Peer> refreshCandidates =
    //             transport.Table.PeersToRefresh(TimeSpan.FromMinutes(1));
    //         Assert.Equal(peersCount, transport.Peers.Count());
    //         Assert.Equal(peersCount / 2, refreshCandidates.Count);
    //         Assert.Equal(peersCount / 2, transport.Table.NonEmptyBuckets.Count());

    //         await transport.Kademlia.RefreshTableAsync(TimeSpan.FromMinutes(1), default);
    //         Assert.NotEqual(
    //             refreshCandidates.ToHashSet(),
    //             transport.Table.PeersToRefresh(TimeSpan.FromMinutes(1)).ToHashSet());
    //         Assert.Equal(
    //             peersCount / 2,
    //             transport.Table.PeersToRefresh(TimeSpan.FromMinutes(1)).Count());
    //         Assert.Equal(peersCount / 2, transport.Table.NonEmptyBuckets.Count());

    //         await transport.Kademlia.RefreshTableAsync(TimeSpan.FromMinutes(1), default);
    //         Assert.Empty(transport.Table.PeersToRefresh(TimeSpan.FromMinutes(1)));
    //     }
    //     finally
    //     {
    //         transport.Dispose();
    //         foreach (var t in transports)
    //         {
    //             t.Dispose();
    //         }
    //     }
    // }

    // private NetMQTransport CreateNetMQTransport(
    //     PrivateKey privateKey = null,
    //     bool blockBroadcast = false,
    //     int tableSize = Kademlia.TableSize,
    //     int bucketSize = Kademlia.BucketSize,
    //     TimeSpan? networkDelay = null)
    // {
    //     return new NetMQTransport(
    //         _transports,
    //         privateKey ?? new PrivateKey(),
    //         blockBroadcast,
    //         tableSize,
    //         bucketSize,
    //         networkDelay);
    // }
}
