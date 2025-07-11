using Libplanet.Net.Messages;

namespace Libplanet.Net.Transports;

internal sealed class NetMQReplyContext(NetMQTransport transport, MessageEnvelope messageEnvelope)
    : IReplyContext
{
    public IMessage Message => messageEnvelope.Message;

    public Protocol Protocol => messageEnvelope.Protocol;

    public Peer Sender => messageEnvelope.Sender;

    public DateTimeOffset Timestamp => messageEnvelope.Timestamp;

    public void Next(IMessage message)
    {
        if (!message.HasNext)
        {
            throw new ArgumentException(
                "The message must have 'HasNext' set to true to be replied.",
                nameof(message));
        }

        transport.Reply(messageEnvelope, message);
    }

    public void Complete(IMessage message)
    {
        if (message.HasNext)
        {
            throw new ArgumentException(
                "The message must have 'HasNext' set to false to be completed.",
                nameof(message));
        }

        transport.Reply(messageEnvelope, message);
    }
}
