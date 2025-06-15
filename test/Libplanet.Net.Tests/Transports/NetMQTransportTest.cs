using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Types;
using NetMQ;
using Serilog;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Transports
{
    [Collection("NetMQConfiguration")]
    public class NetMQTransportTest : TransportTest, IDisposable
    {
        private bool _disposed;

        public NetMQTransportTest(ITestOutputHelper testOutputHelper)
        {
            TransportConstructor = CreateNetMQTransport;

            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffff}[{ThreadId}] - {Message}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithThreadId()
                .WriteTo.TestOutput(testOutputHelper, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<NetMQTransportTest>();
            Logger = Log.ForContext<NetMQTransportTest>();
        }

        ~NetMQTransportTest()
        {
            Dispose(false);
        }

        [Fact]
        public async Task SendMessageAsyncNetMQSocketLeak()
        {
            int previousMaxSocket = NetMQConfig.MaxSockets;

            try
            {
                // An arbitrary number to fit one transport testing.
                NetMQConfig.MaxSockets = 12;
                NetMQTransport transport = new NetMQTransport(
                    new PrivateKey(),
                    new ProtocolOptions(),
                    new HostOptions
                    {
                        Host = IPAddress.Loopback.ToString(),
                    });
                transport.ProcessMessageHandler.Register(
                    async m =>
                    {
                        await transport.ReplyMessageAsync(
                            new PongMessage(),
                            m.Id,
                            CancellationToken.None);
                    });
                await InitializeAsync(transport);

                string invalidHost = Guid.NewGuid().ToString();

                // it isn't assertion for Libplanet codes, but to make sure that `invalidHost`
                // really fails lookup before moving to the next step.
                Assert.ThrowsAny<SocketException>(() =>
                {
                    Dns.GetHostEntry(invalidHost);
                });
                var invalidPeer = new Peer
                {
                    Address = new PrivateKey().Address,
                    EndPoint = new DnsEndPoint(invalidHost, 0),
                };

                InvalidOperationException exc =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => transport.SendMessageAsync(
                            invalidPeer,
                            new PingMessage(),
                            default));

                // Expecting SocketException about host resolving since `invalidPeer` has an
                // invalid hostname
                Assert.IsAssignableFrom<SocketException>(exc.InnerException);

                // Check sending/receiving after exceptions exceeding NetMQConifg.MaxSockets.
                MessageEnvelope reply = await transport.SendMessageAsync(
                    transport.Peer,
                    new PingMessage(),
                    default);
                Assert.IsType<PongMessage>(reply.Message);

                await transport.StopAsync(CancellationToken.None);
            }
            finally
            {
                NetMQConfig.MaxSockets = previousMaxSocket;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    NetMQConfig.Cleanup(false);
                }

                _disposed = true;
            }
        }

        private NetMQTransport CreateNetMQTransport(
            PrivateKey privateKey,
            ProtocolOptions appProtocolVersionOptions,
            HostOptions hostOptions)
        {
            privateKey = privateKey ?? new PrivateKey();
            return new NetMQTransport(
                privateKey,
                appProtocolVersionOptions,
                hostOptions);
        }
    }
}
