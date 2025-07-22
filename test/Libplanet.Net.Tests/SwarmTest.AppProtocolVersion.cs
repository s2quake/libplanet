using System.Collections.Concurrent;
using System.Threading.Tasks;
using Libplanet.Net.Options;
using Libplanet.Types;
using Libplanet.TestUtilities.Extensions;

namespace Libplanet.Net.Tests;

public partial class SwarmTest
{
    [Fact(Timeout = Timeout)]
    public async Task DetectAppProtocolVersion()
    {
        var signer = new PrivateKey();
        var v2 = new SwarmOptions()
        {
            TransportOptions = new TransportOptions()
            {
                Protocol = new ProtocolBuilder { Version = 2 }.Create(signer),
            },
        };
        var v3 = new SwarmOptions
        {
            TransportOptions = new TransportOptions()
            {
                Protocol = new ProtocolBuilder { Version = 3 }.Create(signer),
            },
        };
        await using var a = await CreateSwarm(options: v2);
        await using var b = await CreateSwarm(options: v3);
        await using var c = await CreateSwarm(options: v2);
        await using var d = await CreateSwarm(options: v3);

        await c.StartAsync(default);
        await d.StartAsync(default);

        var peers = new[] { c.Peer, d.Peer };

        foreach (var peer in peers)
        {
            await a.AddPeersAsync([peer], default);
            await b.AddPeersAsync([peer], default);
        }

        Assert.Equal([c.Peer], [.. a.Peers]);
        Assert.Equal([d.Peer], [.. b.Peers]);
    }

    [Fact(Timeout = Timeout)]
    public async Task HandleDifferentAppProtocolVersion()
    {
        var isCalled = false;

        var signer = new PrivateKey();
        var v1 = new SwarmOptions
        {
            TransportOptions = new TransportOptions
            {
                Protocol = new ProtocolBuilder { Version = 1 }.Create(signer),
            },
        };
        var v2 = new SwarmOptions
        {
            TransportOptions = new TransportOptions
            {
                Protocol = new ProtocolBuilder { Version = 2 }.Create(signer),
            },
        };
        await using var a = await CreateSwarm(options: v1);
        await using var b = await CreateSwarm(options: v2);

        await b.StartAsync(default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => a.AddPeersAsync([b.Peer], default));

        Assert.True(isCalled);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreUntrustedAppProtocolVersion()
    {
        var signer = new PrivateKey();
        var older = new ProtocolBuilder { Version = 2 }.Create(signer);
        var newer = new ProtocolBuilder { Version = 3 }.Create(signer);

        var untrustedSigner = new PrivateKey();
        var untrustedOlder = new ProtocolBuilder { Version = 2 }.Create(untrustedSigner);
        var untrustedNewer = new ProtocolBuilder { Version = 3 }.Create(untrustedSigner);

        // _output.WriteLine("Trusted version signer: {0}", signer.Address);
        // _output.WriteLine("Untrusted version signer: {0}", untrustedSigner.Address);

        var logs = new ConcurrentDictionary<Peer, Protocol>();

        void DifferentAppProtocolVersionEncountered(
            Peer peer,
            Protocol peerVersion,
            Protocol localVersion)
        {
            logs[peer] = peerVersion;
        }

        var trustedSigners = new[] { signer.Address }.ToImmutableSortedSet();
        var untrustedSigners = new[] { untrustedSigner.Address }.ToImmutableSortedSet();
        var optionsA = new SwarmOptions
        {
            TransportOptions = new TransportOptions
            {
                Protocol = older,
            },
        };
        await using var a = await CreateSwarm(options: optionsA);
        var optionsB = new SwarmOptions
        {
            TransportOptions = new TransportOptions
            {
                Protocol = newer,
            },
        };
        await using var b = await CreateSwarm(options: optionsB);
        var optionsC = new SwarmOptions
        {
            TransportOptions = new TransportOptions()
            {
                Protocol = older,
            },
        };
        await using var c = await CreateSwarm(options: optionsC);
        var optionsD = new SwarmOptions
        {
            TransportOptions = new TransportOptions
            {
                Protocol = newer,
            },
        };
        await using var d = await CreateSwarm(options: optionsD);
        var optionsE = new SwarmOptions
        {
            TransportOptions = new TransportOptions
            {
                Protocol = untrustedOlder,
            },
        };
        await using var e = await CreateSwarm(options: optionsE);
        var optionsF = new SwarmOptions
        {
            TransportOptions = new TransportOptions
            {
                Protocol = untrustedNewer,
            },
        };
        await using var f = await CreateSwarm(options: optionsF);

        await c.StartAsync(default);
        await d.StartAsync(default);
        await e.StartAsync(default);
        await f.StartAsync(default);

        await a.AddPeersAsync([c.Peer], default);
        await a.AddPeersAsync([d.Peer], default);
        await a.AddPeersAsync([e.Peer], default);
        await a.AddPeersAsync([f.Peer], default);

        await b.AddPeersAsync([c.Peer], default);
        await b.AddPeersAsync([d.Peer], default);
        await b.AddPeersAsync([e.Peer], default);
        await b.AddPeersAsync([f.Peer], default);

        Assert.Equal(new[] { c.Peer }, a.Peers.ToArray());
        Assert.Equal(new[] { d.Peer }, b.Peers.ToArray());

        // _output.WriteLine("Logged encountered peers:");
        // foreach (KeyValuePair<Peer, Protocol> kv in logs)
        // {
        //     _output.WriteLine(
        //         "{0}; {1}; {2} -> {3}",
        //         kv.Key,
        //         kv.Value.Version,
        //         kv.Value.Signer,
        //         kv.Value.Verify() ? "verified" : "not verified");
        // }
    }
}
