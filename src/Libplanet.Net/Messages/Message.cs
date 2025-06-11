namespace Libplanet.Net.Messages;

public sealed record class Message
{
    public required MessageContent Content { get; init; }

    public required Protocol Protocol { get; init; }

    public required Peer Remote { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public byte[] Identity { get; init; } = [];
}
