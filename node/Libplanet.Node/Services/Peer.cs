using System.Diagnostics;
using Libplanet.Net;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Node.Services;

internal sealed class Peer(ITransport transport, Net.Peer boundPeer)
{
    private readonly ITransport _transport = transport;

    public Address Address => BoundPeer.Address;

    public Net.Peer BoundPeer { get; } = boundPeer;

    public DateTimeOffset LastUpdated { get; private set; }

    public DateTimeOffset LifeTime { get; private set; }

    public TimeSpan LifeTimeSpan { get; init; } = TimeSpan.FromSeconds(120);

    public TimeSpan Latency { get; private set; } = TimeSpan.MinValue;

    public bool IsAlive => DateTimeOffset.UtcNow < LifeTime;

    public void Update()
    {
        LastUpdated = DateTimeOffset.UtcNow;
        LifeTime = LastUpdated + LifeTimeSpan;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pingMsg = new PingMessage();
            var stopwatch = Stopwatch.StartNew();
            var replyMessage = await _transport.SendAsync<PongMessage>(BoundPeer, pingMsg, cancellationToken);
            var latency = Stopwatch.GetElapsedTime(stopwatch.ElapsedTicks);
            Latency = latency;
            return true;
        }
        catch
        {
            Latency = TimeSpan.MinValue;
            return false;
        }
    }
}
