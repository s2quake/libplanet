using System.Net;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Tests.Store;
using Libplanet.Types;
using NetMQ;
using Nito.AsyncEx;
using Serilog;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus
{
    [Collection("NetMQConfiguration")]
    public class GossipTest : IDisposable
    {
        private const int Timeout = 60 * 1000;
        private readonly ILogger _logger;

        public GossipTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<GossipTest>();

            _logger = Log.ForContext<GossipTest>();
        }

        public void Dispose()
        {
            NetMQConfig.Cleanup();
        }

        [Fact(Timeout = Timeout)]
        public async Task PublishMessage()
        {
            MemoryRepositoryFixture fx = new MemoryRepositoryFixture();
            bool received1 = false;
            bool received2 = false;
            var key1 = new PrivateKey();
            var key2 = new PrivateKey();
            var receivedEvent = new AsyncAutoResetEvent();
            var gossip1 = CreateGossip(
                content =>
                {
                    if (content is ConsensusProposalMessage)
                    {
                        received1 = true;
                    }
                },
                key1,
                6001,
                new[] { new Peer { Address = key2.Address, EndPoint = new DnsEndPoint("127.0.0.1", 6002) } });
            var gossip2 = CreateGossip(
                content =>
                {
                    if (content is ConsensusProposalMessage)
                    {
                        received2 = true;
                        receivedEvent.Set();
                    }
                },
                key2,
                6002,
                new[] { new Peer { Address = key1.Address, EndPoint = new DnsEndPoint("127.0.0.1", 6001) } });
            try
            {
                await gossip1.StartAsync(default);
                await gossip2.StartAsync(default);
                gossip1.PublishMessage(
                    TestUtils.CreateConsensusPropose(fx.Block1, new PrivateKey(), 0));
                await receivedEvent.WaitAsync();
                Assert.True(received1);
                Assert.True(received2);
            }
            finally
            {
                await gossip1.StopAsync(default);
                await gossip2.StopAsync(default);
                await gossip1.DisposeAsync();
                await gossip2.DisposeAsync();
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task AddMessage()
        {
            // It has no difference with PublishMessage() test,
            // since two methods only has timing difference.
            MemoryRepositoryFixture fx = new MemoryRepositoryFixture();
            bool received1 = false;
            bool received2 = false;
            var key1 = new PrivateKey();
            var key2 = new PrivateKey();
            var receivedEvent = new AsyncAutoResetEvent();
            var gossip1 = CreateGossip(
                content =>
                {
                    if (content is ConsensusProposalMessage)
                    {
                        received1 = true;
                    }
                },
                key1,
                6001,
                [new Peer { Address = key2.Address, EndPoint = new DnsEndPoint("127.0.0.1", 6002) }]);
            var gossip2 = CreateGossip(
                content =>
                {
                    if (content is ConsensusProposalMessage)
                    {
                        received2 = true;
                        receivedEvent.Set();
                    }
                },
                key2,
                6002,
                [new Peer { Address = key1.Address, EndPoint = new DnsEndPoint("127.0.0.1", 6001) }]);
            try
            {
                await gossip1.StartAsync(default);
                await gossip2.StartAsync(default);
                gossip1.AddMessage(
                    TestUtils.CreateConsensusPropose(fx.Block1, new PrivateKey(), 0));
                await receivedEvent.WaitAsync();
                Assert.True(received1);
                Assert.True(received2);
            }
            finally
            {
                await gossip1.StopAsync(default);
                await gossip2.StopAsync(default);
                await gossip1.DisposeAsync();
                await gossip2.DisposeAsync();
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task AddMessages()
        {
            MemoryRepositoryFixture fx = new MemoryRepositoryFixture();
            int received1 = 0;
            int received2 = 0;
            var key1 = new PrivateKey();
            var key2 = new PrivateKey();
            var receivedEvent = new AsyncAutoResetEvent();
            var gossip1 = CreateGossip(
                content =>
                {
                    if (content is ConsensusProposalMessage)
                    {
                        received1++;
                    }
                },
                key1,
                6001,
                [new Peer { Address = key2.Address, EndPoint = new DnsEndPoint("127.0.0.1", 6002) }]);
            var gossip2 = CreateGossip(
                content =>
                {
                    if (content is ConsensusProposalMessage)
                    {
                        received2++;
                    }

                    if (received2 == 4)
                    {
                        receivedEvent.Set();
                    }
                },
                key2,
                6002,
                new[] { new Peer { Address = key1.Address, EndPoint = new DnsEndPoint("127.0.0.1", 6001) } });
            try
            {
                await gossip1.StartAsync(default);
                await gossip2.StartAsync(default);
                PrivateKey key = new PrivateKey();
                gossip1.AddMessages(
                    new[]
                    {
                        TestUtils.CreateConsensusPropose(fx.Block1, key, 0),
                        TestUtils.CreateConsensusPropose(fx.Block1, key, 1),
                        TestUtils.CreateConsensusPropose(fx.Block1, key, 2),
                        TestUtils.CreateConsensusPropose(fx.Block1, key, 3),
                    });

                await receivedEvent.WaitAsync();
                Assert.Equal(4, received1);
                Assert.Equal(4, received2);
            }
            finally
            {
                await gossip1.StopAsync(default);
                await gossip2.StopAsync(default);
                await gossip1.DisposeAsync();
                await gossip2.DisposeAsync();
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task AddPeerWithHaveMessage()
        {
            var key1 = new PrivateKey();
            var key2 = new PrivateKey();
            var received = false;
            var receivedEvent = new AsyncAutoResetEvent();
            var transport1 = CreateTransport(key1, 6001);

            void HandleMessage(MessageEnvelope message)
            {
                received = true;
                receivedEvent.Set();
            }

            transport1.MessageReceived.Subscribe(HandleMessage);
            var gossip = new Gossip(
                transport1,
                ImmutableArray<Peer>.Empty,
                ImmutableArray<Peer>.Empty,
                _ => { },
                _ => { },
                _ => { });
            var transport2 = CreateTransport(key2, 6002);
            try
            {
                await gossip.StartAsync(default);
                await transport2.StartAsync(default);

                await transport2.SendMessageAsync(
                    gossip.AsPeer,
                    new HaveMessage { Ids = [] },
                    default);

                await receivedEvent.WaitAsync();
                Assert.True(received);
                Assert.Contains(transport2.Peer, gossip.Peers);
            }
            finally
            {
                await gossip.StopAsync(default);
                await transport2.StopAsync(default);
                await gossip.DisposeAsync();
                await transport2.DisposeAsync();
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task DoNotBroadcastToSeedPeers()
        {
            bool received = false;
            void ProcessMessage(MessageEnvelope msg)
            {
                if (msg.Message is HaveMessage)
                {
                    received = true;
                }
            }

            ITransport seed = CreateTransport();
            seed.MessageReceived.Subscribe(ProcessMessage);
            Gossip gossip = CreateGossip(_ => { }, seeds: new[] { seed.Peer });

            try
            {
                await seed.StartAsync(default);
                await gossip.StartAsync(default);
                gossip.AddMessage(new PingMessage());

                // Wait heartbeat interval * 2.
                await Task.Delay(2 * 1000);
                Assert.False(received);
            }
            finally
            {
                await seed.StopAsync(default);
                await gossip.StopAsync(default);
                await seed.DisposeAsync();
                await gossip.DisposeAsync();
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task DoNotSendDuplicateMessageRequest()
        {
            int received = 0;
            void ProcessMessage(MessageEnvelope msg)
            {
                if (msg.Message is WantMessage)
                {
                    received++;
                }
            }

            Gossip receiver = CreateGossip(_ => { });
            ITransport sender1 = CreateTransport();
            sender1.MessageReceived.Subscribe(ProcessMessage);
            ITransport sender2 = CreateTransport();
            sender2.MessageReceived.Subscribe(ProcessMessage);

            try
            {
                await receiver.StartAsync(default);
                await sender1.StartAsync(default);
                await sender2.StartAsync(default);
                var msg1 = new PingMessage();
                var msg2 = new PongMessage();
                await sender1.SendMessageAsync(
                    receiver.AsPeer,
                    new HaveMessage { Ids = [msg1.Id, msg2.Id] },
                    default);
                await sender2.SendMessageAsync(
                    receiver.AsPeer,
                    new HaveMessage { Ids = [msg1.Id, msg2.Id] },
                    default);

                // Wait heartbeat interval * 2.
                await Task.Delay(2 * 1000);
                Assert.Equal(1, received);
            }
            finally
            {
                await receiver.StopAsync(default);
                await sender1.StopAsync(default);
                await sender2.StopAsync(default);
                await receiver.DisposeAsync();
                await sender1.DisposeAsync();
                await sender2.DisposeAsync();
            }
        }

        private Gossip CreateGossip(
            Action<IMessage> processMessage,
            PrivateKey? privateKey = null,
            int? port = null,
            IEnumerable<Peer>? peers = null,
            IEnumerable<Peer>? seeds = null)
        {
            var transport = CreateTransport(privateKey, port);
            return new Gossip(
                transport,
                peers?.ToImmutableArray() ?? ImmutableArray<Peer>.Empty,
                seeds?.ToImmutableArray() ?? ImmutableArray<Peer>.Empty,
                _ => { },
                _ => { },
                processMessage);
        }

        private NetMQTransport CreateTransport(
            PrivateKey? privateKey = null,
            int? port = null)
        {
            var apvOptions = new ProtocolOptions
            { Protocol = TestUtils.AppProtocolVersion };
            HostOptions hostOptions;
            if (port is { } p)
            {
                hostOptions = new HostOptions { Host = "127.0.0.1", Port = p };
            }
            else
            {
                hostOptions = new HostOptions { Host = "127.0.0.1" };
            }

            return new NetMQTransport(
                privateKey ?? new PrivateKey(),
                apvOptions,
                hostOptions);
        }
    }
}
