using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "MessageEnvelope")]
public sealed record class MessageEnvelope
{
    [Property(0)]
    public required Guid Identity { get; init; }

    [Property(1)]
    public required IMessage Message { get; init; }

    [Property(2)]
    public required Protocol Protocol { get; init; }

    [Property(3)]
    public required Peer Sender { get; init; }

    [Property(4)]
    public DateTimeOffset Timestamp { get; init; }

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
            throw new InvalidOperationException("");
        }
    }
}
