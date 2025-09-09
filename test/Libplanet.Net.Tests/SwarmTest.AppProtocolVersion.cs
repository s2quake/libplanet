using Libplanet.Net.Components;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using System.Reactive.Linq;
using Libplanet.TestUtilities;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests;

public partial class SwarmTest
{
    [Fact(Timeout = Timeout)]
    public async Task DetectAppProtocolVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var signer = Rand.Signer(random);
        var v2 = new TransportOptions()
        {
            Protocol = Protocol.Create(signer, 2),
        };
        var v3 = new TransportOptions
        {
            Protocol = Protocol.Create(signer, 3),
        };

        var transportA = CreateTransport(options: v2);
        var transportB = CreateTransport(options: v3);
        var transportC = CreateTransport(options: v2);
        var transportD = CreateTransport(options: v3);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peersD = new PeerCollection(transportD.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        using var peerExplorerC = new PeerExplorer(transportC, peersC);
        using var peerExplorerD = new PeerExplorer(transportD, peersD);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
            transportD,
        };

        await transports.StartAsync(cancellationToken);

        var peers = new[] { transportC.Peer, transportD.Peer };

        foreach (var peer in peers)
        {
            await peerExplorerA.PingAsync(peer, cancellationToken);
            await peerExplorerB.PingAsync(peer, cancellationToken);
        }

        Assert.Equal([transportC.Peer, transportD.Peer], peersA.ToHashSet());
        Assert.Equal([transportC.Peer, transportD.Peer], peersB.ToHashSet());
    }

    [Fact(Timeout = Timeout)]
    public async Task HandleDifferentAppProtocolVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var signer = Rand.Signer(random);
        var v1 = new TransportOptions
        {
            Protocol = Protocol.Create(signer, 1),
        };
        var v2 = new TransportOptions
        {
            Protocol = Protocol.Create(signer, 2),
        };
        await using var transportA = CreateTransport(options: v1);
        await using var transportB = CreateTransport(options: v2);
        using var _1 = transportB.MessageRouter.RegisterReceivedMessageValidation<PingMessage>((m, e) =>
        {
            if (e.ProtocolHash != v2.Protocol.Hash)
            {
                throw new InvalidProtocolException("Received message with invalid protocol hash.")
                {
                    ProtocolHash = e.ProtocolHash,
                };
            }
        });

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        var waitTask = transportB.MessageRouter.ReceivedMessageValidationFailed.WaitAsync(
            predicate: e => e.Exception is InvalidProtocolException);
        await Assert.ThrowsAsync<TimeoutException>(() => transportA.PingAsync(transportB.Peer, cancellationToken));
        await waitTask.WaitAsync(WaitTimeout5, cancellationToken);
    }
}
