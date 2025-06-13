using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Libplanet.Types;
using NetMQ;
using Serilog;
using Xunit.Sdk;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Transports
{
    public abstract class TransportTest
    {
        protected const int Timeout = 60 * 1000;

        protected ILogger Logger { get; set; }

        #pragma warning disable MEN002
        protected Func<PrivateKey, ProtocolOptions, HostOptions, Task<ITransport>> TransportConstructor { get; set; }
        #pragma warning restore MEN002

        [SkippableFact(Timeout = Timeout)]
        public async Task StartAsync()
        {
            ITransport transport = await CreateTransportAsync().ConfigureAwait(false);

            try
            {
                await transport.StartAsync(default);
                Assert.True(transport.IsRunning);
            }
            finally
            {
                await transport.StopAsync(default);
                transport.Dispose();
            }
        }

        [SkippableFact(Timeout = Timeout)]
        public async Task RestartAsync()
        {
            ITransport transport = await CreateTransportAsync().ConfigureAwait(false);

            try
            {
                await InitializeAsync(transport);
                Assert.True(transport.IsRunning);
                await transport.StopAsync(default);
                Assert.False(transport.IsRunning);

                await InitializeAsync(transport);
                Assert.True(transport.IsRunning);
            }
            finally
            {
                await transport.StopAsync(default);
                transport.Dispose();
                if (transport is NetMQTransport)
                {
                    NetMQConfig.Cleanup(false);
                }
            }
        }

        [SkippableFact(Timeout = Timeout)]
        public async Task DisposeTest()
        {
            ITransport transport = await CreateTransportAsync().ConfigureAwait(false);

            try
            {
                await InitializeAsync(transport);
                Assert.True(transport.IsRunning);
                await transport.StopAsync(default);
                transport.Dispose();
                var boundPeer = new Peer
                {
                    Address = new PrivateKey().Address,
                    EndPoint = new DnsEndPoint("127.0.0.1", 1234),
                };
                var message = new PingMessage();
                await Assert.ThrowsAsync<ObjectDisposedException>(
                    async () => await transport.StartAsync(default));
                await Assert.ThrowsAsync<ObjectDisposedException>(
                    async () => await transport.StopAsync(default));
                await Assert.ThrowsAsync<ObjectDisposedException>(
                    async () => await transport.SendMessageAsync(
                        boundPeer,
                        message,
                        null,
                        default));
                await Assert.ThrowsAsync<ObjectDisposedException>(
                    async () => await transport.SendMessageAsync(
                        boundPeer,
                        message,
                        null,
                        3,
                        false,
                        default));
                Assert.Throws<ObjectDisposedException>(
                    () => transport.BroadcastMessage(null, message));
                await Assert.ThrowsAsync<ObjectDisposedException>(
                    async () => await transport.ReplyMessageAsync(
                        message,
                        Guid.NewGuid(),
                        default));

                // To check multiple Dispose() throws error or not.
                transport.Dispose();
            }
            finally
            {
                transport.Dispose();
            }
        }

        [SkippableFact(Timeout = Timeout)]
        public async Task AsPeer()
        {
            var privateKey = new PrivateKey();
            string host = IPAddress.Loopback.ToString();
            ITransport transport =
                await CreateTransportAsync(privateKey: privateKey).ConfigureAwait(false);

            try
            {
                var peer = transport.Peer;
                Assert.Equal(privateKey.Address, peer.Address);
                Assert.Equal(host, peer.EndPoint.Host);
            }
            finally
            {
                await transport.StopAsync(default);
                transport.Dispose();
            }
        }

        // This also tests ITransport.ReplyMessageAsync at the same time.
        [SkippableFact(Timeout = Timeout)]
        public async Task SendMessageAsync()
        {
            ITransport transportA = await CreateTransportAsync().ConfigureAwait(false);
            ITransport transportB = await CreateTransportAsync().ConfigureAwait(false);

            transportB.ProcessMessageHandler.Register(async message =>
            {
                if (message.Message is PingMessage)
                {
                    await transportB.ReplyMessageAsync(
                        new PongMessage(),
                        message.Id,
                        CancellationToken.None);
                }
            });

            try
            {
                await InitializeAsync(transportA);
                await InitializeAsync(transportB);

                MessageEnvelope reply = await transportA.SendMessageAsync(
                    transportB.Peer,
                    new PingMessage(),
                    TimeSpan.FromSeconds(3),
                    CancellationToken.None);

                Assert.IsType<PongMessage>(reply.Message);
            }
            finally
            {
                await transportA.StopAsync(default);
                await transportB.StopAsync(default);
                transportA.Dispose();
                transportB.Dispose();
            }
        }

        [SkippableFact(Timeout = Timeout)]
        public async Task SendMessageCancelAsync()
        {
            ITransport transportA = await CreateTransportAsync().ConfigureAwait(false);
            ITransport transportB = await CreateTransportAsync().ConfigureAwait(false);
            var cts = new CancellationTokenSource();

            try
            {
                await InitializeAsync(transportA, default);
                await InitializeAsync(transportB, default);

                cts.CancelAfter(TimeSpan.FromSeconds(1));
                await Assert.ThrowsAsync<TaskCanceledException>(
                    async () => await transportA.SendMessageAsync(
                        transportB.Peer,
                        new PingMessage(),
                        null,
                        cts.Token));
            }
            finally
            {
                await transportA.StopAsync(default);
                await transportB.StopAsync(default);
                transportA.Dispose();
                transportB.Dispose();
                cts.Dispose();
            }
        }

        [SkippableFact(Timeout = Timeout)]
        public async Task SendMessageMultipleRepliesAsync()
        {
            ITransport transportA = await CreateTransportAsync().ConfigureAwait(false);
            ITransport transportB = await CreateTransportAsync().ConfigureAwait(false);

            transportB.ProcessMessageHandler.Register(async message =>
            {
                if (message.Message is PingMessage)
                {
                    await transportB.ReplyMessageAsync(
                        new PingMessage(),
                        message.Id,
                        default);
                    await transportB.ReplyMessageAsync(
                        new PongMessage(),
                        message.Id,
                        default);
                }
            });

            try
            {
                await InitializeAsync(transportA);
                await InitializeAsync(transportB);

                var replies = (await transportA.SendMessageAsync(
                    transportB.Peer,
                    new PingMessage(),
                    TimeSpan.FromSeconds(3),
                    2,
                    false,
                    CancellationToken.None)).ToArray();

                Assert.Contains(replies, message => message.Message is PingMessage);
                Assert.Contains(replies, message => message.Message is PongMessage);
            }
            finally
            {
                await transportA.StopAsync(default);
                await transportB.StopAsync(default);
                transportA.Dispose();
                transportB.Dispose();
            }
        }

        // This also tests ITransport.ReplyMessage at the same time.
        [SkippableFact(Timeout = Timeout)]
        public async Task SendMessageAsyncTimeout()
        {
            ITransport transportA = await CreateTransportAsync().ConfigureAwait(false);
            ITransport transportB = await CreateTransportAsync().ConfigureAwait(false);

            try
            {
                await InitializeAsync(transportA);
                await InitializeAsync(transportB);

                var e = await Assert.ThrowsAsync<CommunicationException>(
                    async () => await transportA.SendMessageAsync(
                        transportB.Peer,
                        new PingMessage(),
                        TimeSpan.FromSeconds(3),
                        CancellationToken.None));
                Assert.True(e.InnerException is TimeoutException ie);
            }
            finally
            {
                await transportA.StopAsync(default);
                await transportB.StopAsync(default);
                transportA.Dispose();
                transportB.Dispose();
            }
        }

        [SkippableTheory(Timeout = Timeout)]
        [ClassData(typeof(TransportTestInvalidPeers))]
        public async Task SendMessageToInvalidPeerAsync(Peer invalidPeer)
        {
            ITransport transport = await CreateTransportAsync().ConfigureAwait(false);

            try
            {
                await InitializeAsync(transport);
                Task task = transport.SendMessageAsync(
                    invalidPeer,
                    new PingMessage(),
                    TimeSpan.FromSeconds(5),
                    default);

                // TcpTransport and NetMQTransport fail for different reasons, i.e.
                // a thrown exception for each case has a different inner exception.
                await Assert.ThrowsAsync<CommunicationException>(async () => await task);
            }
            finally
            {
                await transport.StopAsync(default);
                transport.Dispose();
            }
        }

        [SkippableFact(Timeout = Timeout)]
        public async Task SendMessageAsyncCancelWhenTransportStop()
        {
            ITransport transportA = await CreateTransportAsync().ConfigureAwait(false);
            ITransport transportB = await CreateTransportAsync().ConfigureAwait(false);

            try
            {
                await InitializeAsync(transportA);
                await InitializeAsync(transportB);

                Task t = transportA.SendMessageAsync(
                        transportB.Peer,
                        new PingMessage(),
                        null,
                        CancellationToken.None);

                // For context change
                await Task.Delay(100);

                await transportA.StopAsync(default);
                Assert.False(transportA.IsRunning);
                await Assert.ThrowsAsync<TaskCanceledException>(async () => await t);
                Assert.True(t.IsCanceled);
            }
            finally
            {
                await transportA.StopAsync(default);
                await transportB.StopAsync(default);
                transportA.Dispose();
                transportB.Dispose();
            }
        }

        [SkippableFact(Timeout = Timeout)]
        public async Task BroadcastMessage()
        {
            var address = new PrivateKey().Address;
            ITransport transportA = null;
            ITransport transportB = await CreateTransportAsync(
                privateKey: GeneratePrivateKeyOfBucketIndex(address, 0));
            ITransport transportC = await CreateTransportAsync(
                privateKey: GeneratePrivateKeyOfBucketIndex(address, 1));
            ITransport transportD = await CreateTransportAsync(
                privateKey: GeneratePrivateKeyOfBucketIndex(address, 2));

            var tcsB = new TaskCompletionSource<MessageEnvelope>();
            var tcsC = new TaskCompletionSource<MessageEnvelope>();
            var tcsD = new TaskCompletionSource<MessageEnvelope>();

            transportB.ProcessMessageHandler.Register(MessageHandler(tcsB));
            transportC.ProcessMessageHandler.Register(MessageHandler(tcsC));
            transportD.ProcessMessageHandler.Register(MessageHandler(tcsD));

            Func<MessageEnvelope, Task> MessageHandler(TaskCompletionSource<MessageEnvelope> tcs)
            {
                return async message =>
                {
                    if (message.Message is PingMessage)
                    {
                        tcs.SetResult(message);
                    }

                    await Task.Yield();
                };
            }

            try
            {
                await InitializeAsync(transportB);
                await InitializeAsync(transportC);
                await InitializeAsync(transportD);

                var table = new RoutingTable(address, bucketSize: 1);
                table.AddPeer(transportB.Peer);
                table.AddPeer(transportC.Peer);
                table.AddPeer(transportD.Peer);

                transportA = await CreateTransportAsync().ConfigureAwait(false);
                await InitializeAsync(transportA);

                transportA.BroadcastMessage(
                    table.PeersToBroadcast(transportD.Peer.Address),
                    new PingMessage());

                await Task.WhenAll(tcsB.Task, tcsC.Task);

                Assert.IsType<PingMessage>(tcsB.Task.Result.Message);
                Assert.IsType<PingMessage>(tcsC.Task.Result.Message);
                Assert.False(tcsD.Task.IsCompleted);

                tcsD.SetCanceled();
            }
            finally
            {
                await transportA?.StopAsync(default);
                transportA?.Dispose();
                await transportB.StopAsync(default);
                transportB.Dispose();
                await transportC.StopAsync(default);
                transportC.Dispose();
                await transportD.StopAsync(default);
                transportD.Dispose();
            }
        }

        protected async Task InitializeAsync(
            ITransport transport,
            CancellationToken cts = default)
        {
            await transport.StartAsync(cts);
        }

        private Task<ITransport> CreateTransportAsync(
            PrivateKey privateKey = null,
            ProtocolOptions appProtocolVersionOptions = null,
            HostOptions hostOptions = null,
            TimeSpan? messageTimestampBuffer = null)
        {
            if (TransportConstructor is null)
            {
                throw new XunitException("Transport constructor is not defined.");
            }

            privateKey = privateKey ?? new PrivateKey();
            return TransportConstructor(
                privateKey,
                appProtocolVersionOptions ?? new ProtocolOptions(),
                hostOptions ?? new HostOptions
                {
                    Host = IPAddress.Loopback.ToString(),
                });
        }

        private class TransportTestInvalidPeers : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                // Make sure the tcp port is invalid.
                var l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();

                yield return new[]
                {
                    new Peer
                    {
                        Address = new PrivateKey().Address,
                        EndPoint = new DnsEndPoint("0.0.0.0", port),
                    },
                };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
