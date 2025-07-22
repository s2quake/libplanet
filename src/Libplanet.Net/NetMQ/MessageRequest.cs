using Libplanet.Net.Messages;

namespace Libplanet.Net.NetMQ;

internal sealed record class MessageRequest
{
    public required MessageEnvelope MessageEnvelope { get; init; }

    public required Peer Receiver { get; init; }

    public Guid Identity => MessageEnvelope.Identity;

    public Peer Sender => MessageEnvelope.Sender;
}
