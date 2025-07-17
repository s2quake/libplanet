using Libplanet.Net.Messages;

namespace Libplanet.Net.Transports;

internal sealed record class MessageResponse
{
    public required MessageEnvelope MessageEnvelope { get; init; }

    public required Peer Receiver { get; init; }

    public Guid Identity => MessageEnvelope.Identity;
}
