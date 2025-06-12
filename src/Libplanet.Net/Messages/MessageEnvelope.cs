namespace Libplanet.Net.Messages;

public sealed record class MessageEnvelope
{
    public required IMessage Message { get; init; }

    public required Protocol Protocol { get; init; }

    public required Peer Remote { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public byte[] Identity { get; init; } = [];
}
