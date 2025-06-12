using System.Collections;
using System.Net;
using System.Net.Sockets;
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
        protected Func<PrivateKey, ProtocolOptions, HostOptions, TimeSpan?, Task<ITransport>> TransportConstructor { get; set; }
        #pragma warning restore MEN002

        [SkippableFact(Timeout = Timeout)]
        public async Task StartAsync()
        {
            ITransport transport = await CreateTransportAsync().ConfigureAwait(false);

            try
            {
                _ = transport.StartAsync();
                await transport.WaitForRunningAsync();
                Assert.True(transport.Running);
            }
            finally
            {
                await transport.StopAsync(TimeSpan.FromMilliseconds(100));
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
                Assert.True(transport.Running);
                await transport.StopAsync(TimeSpan.Zero);
                Assert.False(transport.Running);

                await InitializeAsync(transport);
                Assert.True(transport.Running);
            }
            finally
            {
                await transport.StopAsync(TimeSpan.FromMilliseconds(100));
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
                Assert.True(transport.Running);
                await transport.StopAsync(TimeSpan.FromMilliseconds(100));
                transport.Dispose();
                var boundPeer = new Peer
                {
                    Address = new PrivateKey().Address,
                    EndPoint = new DnsEndPoint("127.0.0.1", 1234),
                };
                var message = new PingMessage();
                await Assert.ThrowsAsync<ObjectDisposedException>(
                    async () => await transport.StartAsync());
                await Assert.ThrowsAsync<ObjectDisposedException>(
                    async () => await transport.StopAsync(TimeSpan.Zero));
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
                        Array.Empty<byte>(),
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
                var peer = transport.AsPeer;
                Assert.Equal(privateKey.Address, peer.Address);
                Assert.Equal(host, peer.EndPoint.Host);
            }
            finally
            {
                await transport.StopAsync(TimeSpan.FromMilliseconds(100));
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
                if (message.Content is PingMessage)
                {
                    await transportB.ReplyMessageAsync(
                        new PongMessage(),
                        message.Identity,
                        CancellationToken.None);
                }
            });

            try
            {
                await InitializeAsync(transportA);
                await InitializeAsync(transportB);

                MessageEnvelope reply = await transportA.SendMessageAsync(
                    transportB.AsPeer,
                    new PingMessage(),
                    TimeSpan.FromSeconds(3),
                    CancellationToken.None);

                Assert.IsType<PongMessage>(reply.Content);
            }
            finally
            {
                await transportA.StopAsync(TimeSpan.FromMilliseconds(100));
                await transportB.StopAsync(TimeSpan.FromMilliseconds(100));
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
                        transportB.AsPeer,
                        new PingMessage(),
                        null,
                        cts.Token));
            }
            finally
            {
                await transportA.StopAsync(TimeSpan.FromMilliseconds(100));
                await transportB.StopAsync(TimeSpan.FromMilliseconds(100));
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
                if (message.Content is PingMessage)
                {
                    await transportB.ReplyMessageAsync(
                        new PingMessage(),
                        message.Identity,
                        default);
                    await transportB.ReplyMessageAsync(
                        new PongMessage(),
                        message.Identity,
                        default);
                }
            });

            try
            {
                await InitializeAsync(transportA);
                await InitializeAsync(transportB);

                var replies = (await transportA.SendMessageAsync(
                    transportB.AsPeer,
                    new PingMessage(),
                    TimeSpan.FromSeconds(3),
                    2,
                    false,
                    CancellationToken.None)).ToArray();

                Assert.Contains(replies, message => message.Content is PingMessage);
                Assert.Contains(replies, message => message.Content is PongMessage);
            }
            finally
            {
                await transportA.StopAsync(TimeSpan.FromMilliseconds(100));
                await transportB.StopAsync(TimeSpan.FromMilliseconds(100));
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

                var e = await Assert.ThrowsAsync<CommunicationFailException>(
                    async () => await transportA.SendMessageAsync(
                        transportB.AsPeer,
                        new PingMessage(),
                        TimeSpan.FromSeconds(3),
                        CancellationToken.None));
                Assert.True(e.InnerException is TimeoutException ie);
            }
            finally
            {
                await transportA.StopAsync(TimeSpan.FromMilliseconds(100));
                await transportB.StopAsync(TimeSpan.FromMilliseconds(100));
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
                await Assert.ThrowsAsync<CommunicationFailException>(async () => await task);
            }
            finally
            {
                await transport.StopAsync(TimeSpan.FromMilliseconds(100));
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
                        transportB.AsPeer,
                        new PingMessage(),
                        null,
                        CancellationToken.None);

                // For context change
                await Task.Delay(100);

                await transportA.StopAsync(TimeSpan.Zero);
                Assert.False(transportA.Running);
                await Assert.ThrowsAsync<TaskCanceledException>(async () => await t);
                Assert.True(t.IsCanceled);
            }
            finally
            {
                await transportA.StopAsync(TimeSpan.FromMilliseconds(100));
                await transportB.StopAsync(TimeSpan.FromMilliseconds(100));
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
                    if (message.Content is PingMessage)
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
                table.AddPeer(transportB.AsPeer);
                table.AddPeer(transportC.AsPeer);
                table.AddPeer(transportD.AsPeer);

                transportA = await CreateTransportAsync().ConfigureAwait(false);
                await InitializeAsync(transportA);

                transportA.BroadcastMessage(
                    table.PeersToBroadcast(transportD.AsPeer.Address),
                    new PingMessage());

                await Task.WhenAll(tcsB.Task, tcsC.Task);

                Assert.IsType<PingMessage>(tcsB.Task.Result.Content);
                Assert.IsType<PingMessage>(tcsC.Task.Result.Content);
                Assert.False(tcsD.Task.IsCompleted);

                tcsD.SetCanceled();
            }
            finally
            {
                await transportA?.StopAsync(TimeSpan.FromMilliseconds(100));
                transportA?.Dispose();
                await transportB.StopAsync(TimeSpan.FromMilliseconds(100));
                transportB.Dispose();
                await transportC.StopAsync(TimeSpan.FromMilliseconds(100));
                transportC.Dispose();
                await transportD.StopAsync(TimeSpan.FromMilliseconds(100));
                transportD.Dispose();
            }
        }

        protected async Task InitializeAsync(
            ITransport transport,
            CancellationToken cts = default)
        {
            _ = transport.StartAsync(cts);
            await transport.WaitForRunningAsync();
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
                },
                messageTimestampBuffer);
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
