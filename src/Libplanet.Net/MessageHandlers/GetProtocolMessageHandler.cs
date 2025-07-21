using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class GetProtocolMessageHandler(ITransport transport)
    : MessageHandlerBase<GetProtocolMessage>
{
    protected override void OnHandle(
        GetProtocolMessage message, MessageEnvelope messageEnvelope)
    {
        var replyMessage = new ProtocolMessage
        {
            Protocol = transport.Protocol,
        };
        transport.Post(messageEnvelope.Sender, replyMessage, messageEnvelope.ReplyTo);
    }
}
