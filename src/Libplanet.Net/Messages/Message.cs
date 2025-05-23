namespace Libplanet.Net.Messages;

public class Message
{
    public Message(
        MessageContent content,
        Protocol version,
        BoundPeer remote,
        DateTimeOffset timestamp,
        byte[]? identity)
    {
        Content = content;
        Version = version;
        Remote = remote;
        Timestamp = timestamp;
        Identity = identity;
    }

    public MessageContent Content { get; }

    public Protocol Version { get; }

    public BoundPeer Remote { get; }

    public DateTimeOffset Timestamp { get; }

    public byte[]? Identity { get; }
}
