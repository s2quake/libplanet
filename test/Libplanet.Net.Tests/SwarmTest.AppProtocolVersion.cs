using System.Collections.Concurrent;
using System.Threading.Tasks;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Types;

namespace Libplanet.Net.Tests
{
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
                    Protocol = Protocol.Create(signer, 2),
                },
            };
            var v3 = new SwarmOptions
            {
                TransportOptions = new TransportOptions()
                {
                    Protocol = Protocol.Create(signer, 3),
                },
            };
            var a = await CreateSwarm(options: v2);
            var b = await CreateSwarm(options: v3);
            var c = await CreateSwarm(options: v2);
            var d = await CreateSwarm(options: v3);

            try
            {
                await StartAsync(c);
                await StartAsync(d);

                var peers = new[] { c.Peer, d.Peer };

                foreach (var peer in peers)
                {
                    await a.AddPeersAsync(new[] { peer }, default);
                    await b.AddPeersAsync(new[] { peer }, default);
                }

                Assert.Equal(new[] { c.Peer }, a.Peers.ToArray());
                Assert.Equal(new[] { d.Peer }, b.Peers.ToArray());
            }
            finally
            {
                await StopAsync(c);
                await StopAsync(d);

                await a.DisposeAsync();
                await b.DisposeAsync();
                await c.DisposeAsync();
                await d.DisposeAsync();
            }
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
                    Protocol = Protocol.Create(signer, 1),
                },
            };
            var v2 = new SwarmOptions
            {
                TransportOptions = new TransportOptions
                {
                    Protocol = Protocol.Create(signer, 2),
                },
            };
            var a = await CreateSwarm(options: v1);
            var b = await CreateSwarm(options: v2);

            try
            {
                await StartAsync(b);

                await Assert.ThrowsAsync<InvalidOperationException>(() => BootstrapAsync(a, b.Peer));

                Assert.True(isCalled);
            }
            finally
            {
                await StopAsync(a);
                await StopAsync(b);

                await a.DisposeAsync();
                await b.DisposeAsync();
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task IgnoreUntrustedAppProtocolVersion()
        {
            var signer = new PrivateKey();
            Protocol older = Protocol.Create(signer, 2);
            Protocol newer = Protocol.Create(signer, 3);

            var untrustedSigner = new PrivateKey();
            Protocol untrustedOlder = Protocol.Create(untrustedSigner, 2);
            Protocol untrustedNewer = Protocol.Create(untrustedSigner, 3);

            _output.WriteLine("Trusted version signer: {0}", signer.Address);
            _output.WriteLine("Untrusted version signer: {0}", untrustedSigner.Address);

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
            var a = await CreateSwarm(options: optionsA);
            var optionsB = new SwarmOptions
            {
                TransportOptions = new TransportOptions
                {
                    Protocol = newer,
                },
            };
            var b = await CreateSwarm(options: optionsB);
            var optionsC = new SwarmOptions
            {
                TransportOptions = new TransportOptions()
                {
                    Protocol = older,
                },
            };
            var c = await CreateSwarm(options: optionsC);
            var optionsD = new SwarmOptions
            {
                TransportOptions = new TransportOptions
                {
                    Protocol = newer,
                },
            };
            var d = await CreateSwarm(options: optionsD);
            var optionsE = new SwarmOptions
            {
                TransportOptions = new TransportOptions
                {
                    Protocol = untrustedOlder,
                },
            };
            var e = await CreateSwarm(options: optionsE);
            var optionsF = new SwarmOptions
            {
                TransportOptions = new TransportOptions
                {
                    Protocol = untrustedNewer,
                },
            };
            var f = await CreateSwarm(options: optionsF);

            try
            {
                await StartAsync(c);
                await StartAsync(d);
                await StartAsync(e);
                await StartAsync(f);

                await a.AddPeersAsync(new[] { c.Peer }, default);
                await a.AddPeersAsync(new[] { d.Peer }, default);
                await a.AddPeersAsync(new[] { e.Peer }, default);
                await a.AddPeersAsync(new[] { f.Peer }, default);

                await b.AddPeersAsync(new[] { c.Peer }, default);
                await b.AddPeersAsync(new[] { d.Peer }, default);
                await b.AddPeersAsync(new[] { e.Peer }, default);
                await b.AddPeersAsync(new[] { f.Peer }, default);

                Assert.Equal(new[] { c.Peer }, a.Peers.ToArray());
                Assert.Equal(new[] { d.Peer }, b.Peers.ToArray());

                _output.WriteLine("Logged encountered peers:");
                foreach (KeyValuePair<Peer, Protocol> kv in logs)
                {
                    _output.WriteLine(
                        "{0}; {1}; {2} -> {3}",
                        kv.Key,
                        kv.Value.Version,
                        kv.Value.Signer,
                        kv.Value.Verify() ? "verified" : "not verified");
                }
            }
            finally
            {
                await StopAsync(c);
                await StopAsync(d);
                await StopAsync(e);
                await StopAsync(f);

                await a.DisposeAsync();
                await b.DisposeAsync();
                await c.DisposeAsync();
                await d.DisposeAsync();
                await e.DisposeAsync();
                await f.DisposeAsync();
            }
        }
    }
}
