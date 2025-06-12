using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Tests.Store;
using Libplanet.Types;
using NetMQ;
using Xunit.Sdk;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Net.Tests.Transports
{
    [Collection("NetMQConfiguration")]
    public class BoundPeerExtensionsTest : IDisposable
    {
        public void Dispose()
        {
            NetMQConfig.Cleanup(false);
        }

        [Fact(Timeout = 60 * 1000)]
        public async Task QueryAppProtocolVersion()
        {
            var policy = new BlockchainOptions();
            var fx = new MemoryRepositoryFixture(policy);
            var blockchain = MakeBlockChain(policy);
            var swarmKey = new PrivateKey();
            var consensusKey = new PrivateKey();
            var validators = new List<PublicKey>()
            {
                swarmKey.PublicKey,
            };
            var apv = Protocol.Create(new PrivateKey(), 1);
            var apvOptions = new ProtocolOptions() { Protocol = apv };
            string host = IPAddress.Loopback.ToString();
            int port = FreeTcpPort();
            var hostOptions = new HostOptions
            {
                Host = IPAddress.Loopback.ToString(),
                Port = port,
            };
            var option = new SwarmOptions();
            var transport = await NetMQTransport.Create(
                swarmKey,
                apvOptions,
                hostOptions);
            using (var swarm = new Swarm(
                blockchain,
                swarmKey,
                transport,
                options: option))
            {
                var peer = new Peer { Address = swarmKey.Address, EndPoint = new DnsEndPoint(host, port) };
                // Before swarm starting...
                Assert.Throws<TimeoutException>(() =>
                {
                    if (swarm.Transport is NetMQTransport)
                    {
                        peer.QueryAppProtocolVersionNetMQ(timeout: TimeSpan.FromSeconds(1));
                    }
                    else
                    {
                        throw new XunitException(
                            "Each type of transport must have corresponding test case.");
                    }
                });
                _ = swarm.StartAsync();
                try
                {
                    Protocol receivedAPV = default;
                    if (swarm.Transport is NetMQTransport)
                    {
                        receivedAPV = peer.QueryAppProtocolVersionNetMQ(
                            timeout: TimeSpan.FromSeconds(1));
                    }
                    else
                    {
                        throw new XunitException(
                            "Each type of transport must have corresponding test case.");
                    }

                    Assert.Equal(apv, receivedAPV);
                }
                finally
                {
                    await swarm.StopAsync();
                }
            }

            NetMQConfig.Cleanup(false);
        }

        [Theory]
        [InlineData("127.0.0.1", 3000, new[] { "tcp://127.0.0.1:3000" })]
        [InlineData("127.0.0.1", 3000, new[] { "tcp://127.0.0.1:3000", "tcp://::1:3000" })]
        public async Task ResolveNetMQAddressAsync(string host, int port, string[] expected)
        {
            var bp = new Peer
            {
                Address = new PrivateKey().Address,
                EndPoint = new DnsEndPoint(host, port),
            };
            var addr = await bp.ResolveNetMQAddressAsync(default);

            Assert.Contains(addr, expected);
        }

        [Fact]
        public async Task ResolveNetMQAddressAsyncFails()
        {
            string hostDoesNotExist = $"{Guid.NewGuid()}.com";
            var bp = new Peer
            {
                Address = new PrivateKey().Address,
                EndPoint = new DnsEndPoint(hostDoesNotExist, 3000),
            };
            await Assert.ThrowsAnyAsync<SocketException>(async () =>
            {
                await bp.ResolveNetMQAddressAsync(default);
            });
        }

        private static int FreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
