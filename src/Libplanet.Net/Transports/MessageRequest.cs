using System.Threading;
using System.Threading.Channels;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Transports;

internal sealed record class MessageRequest
{
    public required MessageEnvelope MessageEnvelope { get; init; }

    public required Peer Receiver { get; init; }

    // public Channel<MessageResponse>? Channel { get; init; }

    // public CancellationToken CancellationToken { get; init; }

    public Guid Identity => MessageEnvelope.Identity;

    public Peer Sender => MessageEnvelope.Sender;
}
