using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.TestUtilities;
using Libplanet.Types;
using NetMQ;
using Serilog;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Transports;

[Collection("NetMQConfiguration")]
public sealed class NetMQTransportTest(ITestOutputHelper output) : TransportTest(output), IDisposable
{
    private bool _disposed;

    // public NetMQTransportTest(ITestOutputHelper testOutputHelper)
    // {
    //     TransportConstructor = CreateNetMQTransport;

    //     const string outputTemplate =
    //         "{Timestamp:HH:mm:ss:ffffff}[{ThreadId}] - {Message}";
    //     Log.Logger = new LoggerConfiguration()
    //         .MinimumLevel.Verbose()
    //         .Enrich.WithThreadId()
    //         .WriteTo.TestOutput(testOutputHelper, outputTemplate: outputTemplate)
    //         .CreateLogger()
    //         .ForContext<NetMQTransportTest>();
    //     Logger = Log.ForContext<NetMQTransportTest>();
    // }

    [Fact]
    public async Task SendMessageAsyncNetMQSocketLeak()
    {
        using var scope = new PropertyScope(typeof(NetMQConfig), nameof(NetMQConfig.MaxSockets), 12);

        await using var transport = new NetMQTransport(new PrivateKey().AsSigner());
        using var _ = transport.ProcessMessage.Subscribe(transport.Pong);
        var invalidHost = Guid.NewGuid().ToString();
        var invalidPeer = new Peer
        {
            Address = new PrivateKey().Address,
            EndPoint = new DnsEndPoint(invalidHost, 0),
        };

        await transport.StartAsync(default);

        // it isn't assertion for Libplanet codes, but to make sure that `invalidHost`
        // really fails lookup before moving to the next step.
        Assert.ThrowsAny<SocketException>(() => Dns.GetHostEntry(invalidHost));

        var exc = await Assert.ThrowsAsync<CommunicationException>(
            () => transport.SendMessageAsync(invalidPeer, new PingMessage(), default));

        // Expecting SocketException about host resolving since `invalidPeer` has an
        // invalid hostname
        Assert.IsType<ChannelClosedException>(exc.InnerException, exactMatch: false);
        Assert.IsType<SocketException>(exc.InnerException?.InnerException, exactMatch: false);

        // Check sending/receiving after exceptions exceeding NetMQConifg.MaxSockets.
        var reply = await transport.SendMessageAsync(transport.Peer, new PingMessage(), default);
        Assert.IsType<PongMessage>(reply.Message);
    }

    [Fact]
    public async Task SendMessageAsStreamAsync()
    {
        var random = RandomUtility.GetRandom(output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);

        using var subscription = transportB.ProcessMessage.Subscribe(async messageEnvelope =>
        {
            if (messageEnvelope.Message is PingMessage)
            {
                transportB.ReplyMessage(messageEnvelope.Identity, new PingMessage { HasNext = true });
                await Task.Delay(100, default);
                transportB.ReplyMessage(messageEnvelope.Identity, new PongMessage());
            }
        });

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var replyMessage = await transportA.SendMessageAsStreamAsync(transportB.Peer, new PingMessage(), default)
            .ToArrayAsync();
        Assert.Equal(2, replyMessage.Length);
        Assert.IsType<PingMessage>(replyMessage[0]);
        Assert.IsType<PongMessage>(replyMessage[1]);
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
                // NetMQConfig.Cleanup(false);
            }

            _disposed = true;
        }
    }

    protected override ITransport CreateTransport(PrivateKey privateKey, TransportOptions transportOptions)
        => new NetMQTransport(privateKey.AsSigner(), transportOptions);
}
