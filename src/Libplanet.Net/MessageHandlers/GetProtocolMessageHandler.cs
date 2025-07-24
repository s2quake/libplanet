using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class GetProtocolMessageHandler(ITransport transport)
    : MessageHandlerBase<ProtocolRequestMessage>
{
    protected override void OnHandle(
        ProtocolRequestMessage message, MessageEnvelope messageEnvelope)
    {
        var replyMessage = new ProtocolResponseMessage
        {
            Protocol = transport.Protocol,
        };
        transport.Post(messageEnvelope.Sender, replyMessage, messageEnvelope.ReplyTo);
    }
}
