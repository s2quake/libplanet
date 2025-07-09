using System.Threading.Tasks;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
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

    // [Fact(Timeout = Timeout)]
    // public async Task PingTwice()
    // {
    //     var transportA = CreateNetMQTransport();
    //     var transportB = CreateNetMQTransport();

    //     try
    //     {
    //         await StartNetMQTransportAsync(transportA);
    //         await StartNetMQTransportAsync(transportB);

    //         transportA.SendPing(transportB.Peer);
    //         await transportA.MessageReceived.WaitAsync();
    //         await transportB.MessageReceived.WaitAsync();
    //         transportB.SendPing(transportA.Peer);
    //         await transportA.MessageReceived.WaitAsync();
    //         await transportB.MessageReceived.WaitAsync();

    //         Assert.Equal(2, transportA.ReceivedMessages.Count);
    //         Assert.Equal(2, transportB.ReceivedMessages.Count);
    //         Assert.Contains(transportA.Peer, transportB.Peers);
    //         Assert.Contains(transportB.Peer, transportA.Peers);
    //     }
    //     finally
    //     {
    //         await transportA.StopAsync(default);
    //         await transportB.StopAsync(default);
    //     }
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task PingToClosedPeer()
    // {
    //     var transportA = CreateNetMQTransport();
    //     var transportB = CreateNetMQTransport();
    //     var transportC = CreateNetMQTransport();

    //     await StartNetMQTransportAsync(transportA);
    //     await StartNetMQTransportAsync(transportB);
    //     await StartNetMQTransportAsync(transportC);

    //     await transportA.AddPeersAsync(new[] { transportB.Peer, transportC.Peer }, null);

    //     Assert.Contains(transportB.Peer, transportA.Peers);
    //     Assert.Contains(transportC.Peer, transportA.Peers);

    //     await transportC.StopAsync(default);
    //     await Assert.ThrowsAsync<TimeoutException>(
    //         () => transportA.AddPeersAsync(
    //             new[] { transportC.Peer },
    //             TimeSpan.FromSeconds(3)));
    //     await transportA.AddPeersAsync(new[] { transportB.Peer }, null);

    //     Assert.Contains(transportB.Peer, transportA.Peers);

    //     transportA.Dispose();
    //     transportB.Dispose();
    //     transportC.Dispose();
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task BootstrapException()
    // {
    //     var transportA = CreateNetMQTransport();
    //     var transportB = CreateNetMQTransport();

    //     await Assert.ThrowsAsync<InvalidOperationException>(
    //         () => transportB.BootstrapAsync(
    //             new[] { transportA.Peer },
    //             TimeSpan.FromSeconds(3)));

    //     transportA.Dispose();
    //     transportB.Dispose();
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task BootstrapAsyncTest()
    // {
    //     var transportA = CreateNetMQTransport();
    //     var transportB = CreateNetMQTransport();
    //     var transportC = CreateNetMQTransport();

    //     try
    //     {
    //         await StartNetMQTransportAsync(transportA);
    //         await StartNetMQTransportAsync(transportB);
    //         await StartNetMQTransportAsync(transportC);

    //         await transportB.BootstrapAsync(new[] { transportA.Peer });
    //         await transportC.BootstrapAsync(new[] { transportA.Peer });

    //         Assert.Contains(transportB.Peer, transportC.Peers);
    //         Assert.Contains(transportC.Peer, transportB.Peers);

    //         transportA.Table.Clear();
    //         transportB.Table.Clear();
    //         transportC.Table.Clear();

    //         await transportB.AddPeersAsync(new[] { transportC.Peer }, null);
    //         await transportC.StopAsync(default);
    //         await transportA.BootstrapAsync(new[] { transportB.Peer });
    //         Assert.Contains(transportB.Peer, transportA.Peers);
    //         Assert.DoesNotContain(transportC.Peer, transportA.Peers);
    //     }
    //     finally
    //     {
    //         transportA.Dispose();
    //         transportB.Dispose();
    //         transportC.Dispose();
    //     }
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task RemoveStalePeers()
    // {
    //     var transportA = CreateNetMQTransport();
    //     var transportB = CreateNetMQTransport();

    //     await StartNetMQTransportAsync(transportA);
    //     await StartNetMQTransportAsync(transportB);

    //     await transportA.AddPeersAsync(new[] { transportB.Peer }, null);
    //     Assert.Single(transportA.Peers);

    //     await transportB.StopAsync(default);
    //     await Task.Delay(100);
    //     await transportA.Kademlia.RefreshTableAsync(TimeSpan.Zero, default);
    //     Assert.Empty(transportA.Peers);

    //     transportA.Dispose();
    //     transportB.Dispose();
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task RoutingTableFull()
    // {
    //     var transport = CreateNetMQTransport(tableSize: 1, bucketSize: 1);
    //     var transportA = CreateNetMQTransport();
    //     var transportB = CreateNetMQTransport();
    //     var transportC = CreateNetMQTransport();

    //     await StartNetMQTransportAsync(transport);
    //     await StartNetMQTransportAsync(transportA);
    //     await StartNetMQTransportAsync(transportB);
    //     await StartNetMQTransportAsync(transportC);

    //     await transportA.AddPeersAsync(new[] { transport.Peer }, null);
    //     await transportB.AddPeersAsync(new[] { transport.Peer }, null);
    //     await transportC.AddPeersAsync(new[] { transport.Peer }, null);

    //     Assert.Single(transportA.Peers);
    //     Assert.Contains(transportA.Peer, transport.Peers);
    //     Assert.DoesNotContain(transportB.Peer, transport.Peers);
    //     Assert.DoesNotContain(transportC.Peer, transport.Peers);

    //     transport.Dispose();
    //     transportA.Dispose();
    //     transportB.Dispose();
    //     transportC.Dispose();
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task ReplacementCache()
    // {
    //     var transport = CreateNetMQTransport(tableSize: 1, bucketSize: 1);
    //     var transportA = CreateNetMQTransport();
    //     var transportB = CreateNetMQTransport();
    //     var transportC = CreateNetMQTransport();

    //     await StartNetMQTransportAsync(transport);
    //     await StartNetMQTransportAsync(transportA);
    //     await StartNetMQTransportAsync(transportB);
    //     await StartNetMQTransportAsync(transportC);

    //     await transportA.AddPeersAsync(new[] { transport.Peer }, null);
    //     await transportB.AddPeersAsync(new[] { transport.Peer }, null);
    //     await Task.Delay(100);
    //     await transportC.AddPeersAsync(new[] { transport.Peer }, null);

    //     Assert.Single(transportA.Peers);
    //     Assert.Contains(transportA.Peer, transport.Peers);
    //     Assert.DoesNotContain(transportB.Peer, transport.Peers);
    //     Assert.DoesNotContain(transportC.Peer, transport.Peers);

    //     await transportA.StopAsync(default);
    //     await transport.Kademlia.RefreshTableAsync(TimeSpan.Zero, default);
    //     await transport.Kademlia.CheckReplacementCacheAsync(default);

    //     Assert.Single(transport.Peers);
    //     Assert.DoesNotContain(transportA.Peer, transport.Peers);
    //     Assert.DoesNotContain(transportB.Peer, transport.Peers);
    //     Assert.Contains(transportC.Peer, transport.Peers);

    //     transport.Dispose();
    //     transportA.Dispose();
    //     transportB.Dispose();
    //     transportC.Dispose();
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task RemoveDeadReplacementCache()
    // {
    //     var transport = CreateNetMQTransport(tableSize: 1, bucketSize: 1);
    //     var transportA = CreateNetMQTransport();
    //     var transportB = CreateNetMQTransport();
    //     var transportC = CreateNetMQTransport();

    //     await StartNetMQTransportAsync(transport);
    //     await StartNetMQTransportAsync(transportA);
    //     await StartNetMQTransportAsync(transportB);
    //     await StartNetMQTransportAsync(transportC);

    //     await transportA.AddPeersAsync(new[] { transport.Peer }, null);
    //     await transportB.AddPeersAsync(new[] { transport.Peer }, null);

    //     Assert.Single(transport.Peers);
    //     Assert.Contains(transportA.Peer, transport.Peers);
    //     Assert.DoesNotContain(transportB.Peer, transport.Peers);

    //     await transportA.StopAsync(default);
    //     await transportB.StopAsync(default);

    //     await transportC.AddPeersAsync(new[] { transport.Peer }, null);
    //     await transport.Kademlia.RefreshTableAsync(TimeSpan.Zero, default);
    //     await transport.Kademlia.CheckReplacementCacheAsync(default);

    //     Assert.Single(transport.Peers);
    //     Assert.DoesNotContain(transportA.Peer, transport.Peers);
    //     Assert.DoesNotContain(transportB.Peer, transport.Peers);
    //     Assert.Contains(transportC.Peer, transport.Peers);

    //     transport.Dispose();
    //     transportA.Dispose();
    //     transportB.Dispose();
    //     transportC.Dispose();
    // }

    // [Theory(Timeout = 2 * Timeout)]
    // [InlineData(1)]
    // [InlineData(5)]
    // [InlineData(20)]
    // [InlineData(50)]
    // public async Task BroadcastMessage(int count)
    // {
    //     var seed = CreateNetMQTransport();
    //     await StartNetMQTransportAsync(seed);
    //     var transports = new NetMQTransport[count];
    //     for (var i = 0; i < count; i++)
    //     {
    //         transports[i] = CreateNetMQTransport();
    //         await StartNetMQTransportAsync(transports[i]);
    //     }

    //     try
    //     {
    //         foreach (var transport in transports)
    //         {
    //             await transport.BootstrapAsync(new[] { seed.Peer });
    //         }

    //         Log.Debug("Bootstrap completed");

    //         var tasks =
    //             transports.Select(transport => transport.WaitForTestMessageWithData("foo"));

    //         seed.BroadcastTestMessage(default, "foo");
    //         Log.Debug("Broadcast completed");

    //         await Task.WhenAll(tasks);
    //     }
    //     finally
    //     {
    //         seed.Dispose();
    //         foreach (var transport in transports)
    //         {
    //             Assert.True(transport.ReceivedTestMessageOfData("foo"));
    //             transport.Dispose();
    //         }
    //     }
    // }

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
