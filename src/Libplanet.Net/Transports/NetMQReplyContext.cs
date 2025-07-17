// using System.Threading.Tasks;
// using Libplanet.Net.Messages;

// namespace Libplanet.Net.Transports;

// internal sealed class NetMQReplyContext(NetMQTransport transport, MessageEnvelope messageEnvelope)
//     : IReplyContext
// {
//     public IMessage Message => messageEnvelope.Message;

//     public Protocol Protocol => messageEnvelope.Protocol;

//     public Peer Sender => messageEnvelope.Sender;

//     public DateTimeOffset Timestamp => messageEnvelope.Timestamp;

//     public ValueTask NextAsync(IMessage message) => transport.ReplyAsync(messageEnvelope, message, hasNext: true);

//     public ValueTask CompleteAsync(IMessage message) => transport.ReplyAsync(messageEnvelope, message, hasNext: false);
// }
