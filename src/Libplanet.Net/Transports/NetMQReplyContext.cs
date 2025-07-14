using Libplanet.Net.Messages;

namespace Libplanet.Net.Transports;

internal sealed class NetMQReplyContext(NetMQTransport transport, MessageEnvelope messageEnvelope)
    : IReplyContext
{
    public IMessage Message => messageEnvelope.Message;

    public Protocol Protocol => messageEnvelope.Protocol;

    public Peer Sender => messageEnvelope.Sender;

    public DateTimeOffset Timestamp => messageEnvelope.Timestamp;

    public void Next(IMessage message) => transport.Reply(messageEnvelope, message, hasNext: true);

    public void Complete(IMessage message) => transport.Reply(messageEnvelope, message, hasNext: false);
}
