using Libplanet.Net.Messages;
using Microsoft.Extensions.Logging;

namespace Libplanet.Net.NetMQ;

public sealed partial class NetMQTransport
{
    private static void LogMessageReceived(ILogger logger, MessageEnvelope messageEnvelope)
    {
        LogMessageReceived(
            logger,
            messageEnvelope.Identity,
            messageEnvelope.Sender,
            messageEnvelope.Message.Id,
            messageEnvelope.ReplyTo);
    }

    private static void LogMessageSent(
        ILogger logger, MessageEnvelope messageEnvelope, Peer receiver)
    {
        LogMessageSent(
            logger,
            messageEnvelope.Identity,
            receiver,
            messageEnvelope.Message.Id);
    }

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "Message received: {Identity}, {Sender}, {MessageId}, {ReplyTo}")]
    private static partial void LogMessageReceived(ILogger logger, Guid identity, Peer sender, MessageId messageId, Guid? replyTo);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "Message sent: {Identity}, {Receiver}, {MessageId}")]
    private static partial void LogMessageSent(ILogger logger, Guid identity, Peer receiver, MessageId messageId);
}
