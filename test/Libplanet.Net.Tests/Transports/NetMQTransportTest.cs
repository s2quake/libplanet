#pragma warning disable S1751 // Loops with at most one iteration should be refactored
using Libplanet.Net.Messages;
using Libplanet.Net.NetMQ;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Transports;

[Collection("NetMQConfiguration")]
public sealed class NetMQTransportTest(ITestOutputHelper output)
    : TransportTestBase(output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task SendAndWaitAsync2_AsStream()
    {
        var random = RandomUtility.GetRandom(_output);
        await using var transportA = CreateTransport(random);
        await using var transportB = CreateTransport(random);
        transportB.MessageRouter.Register<PingMessage>(async (m, e) =>
        {
            for (var i = 0; i < 10; i++)
            {
                transportB.Post(e.Sender, new PingMessage(), e.Identity);
                await Task.Delay(100, default);
            }
            transportB.Post(e.Sender, new PongMessage(), e.Identity);
        });

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var peer = transportB.Peer;
        var message = new PingMessage();
        var isLast = new Func<IMessage, bool>(m => m is PongMessage);
        var query = transportA.SendAsync(peer, message, isLast, default);
        var messageList = new List<IMessage>();
        await foreach (var item in query)
        {
            messageList.Add(item);
        }

        Assert.Equal(11, messageList.Count);
        Assert.All(messageList.Take(10), m => Assert.IsType<PingMessage>(m));
        Assert.IsType<PongMessage>(messageList[^1]);
    }

    protected override ITransport CreateTransport(PrivateKey privateKey, TransportOptions transportOptions)
        => new NetMQTransport(privateKey.AsSigner(), transportOptions);
}
