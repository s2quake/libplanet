using Libplanet.Net.Transports;

namespace Libplanet.Net.Messages;

public sealed record class MessageEnvelope
{
    public required IMessage Message { get; init; }

    public required Protocol Protocol { get; init; }

    public required Peer Remote { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public byte[] Identity { get; init; } = [];

    public void Validate(TimeSpan lifetime)
    {
        if (lifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Lifetime must be non-negative.");
        }

        var timestamp = DateTimeOffset.UtcNow;
        if ((timestamp - Timestamp) > lifetime)
        {
            throw new InvalidMessageTimestampException(Timestamp, lifetime, timestamp);
        }
    }
}
