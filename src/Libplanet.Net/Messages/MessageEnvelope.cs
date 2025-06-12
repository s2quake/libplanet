using Libplanet.Net.Transports;

namespace Libplanet.Net.Messages;

public sealed record class MessageEnvelope
{
    public required IMessage Message { get; init; }

    public required Protocol Protocol { get; init; }

    public required Peer Remote { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public byte[] Identity { get; init; } = [];

    public void Validate(Protocol protocol, TimeSpan lifetime)
    {
        if (lifetime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Lifetime must be non-negative.");
        }

        if (!Protocol.Equals(protocol))
        {
            throw new InvalidOperationException("The protocol of the message does not match the expected one.");
        }

        var timestamp = DateTimeOffset.UtcNow;
        if ((timestamp - Timestamp) > lifetime)
        {
            throw new InvalidMessageTimestampException(Timestamp, lifetime, timestamp);
        }
    }
}
