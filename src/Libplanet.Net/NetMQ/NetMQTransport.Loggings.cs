using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet.Net.NetMQ;

public sealed partial class NetMQTransport
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "NetMQTransport started: {Signer}, Host: {Host}, Port: {Port}")]
    private static partial void LogStarted(ILogger logger, Address signer, string host, int port);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "NetMQTransport stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "Message received: {Identity}, {Sender}, {MessageId}")]
    private static partial void LogMessageReceived(ILogger logger, Guid identity, Peer sender, MessageId messageId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "Message sent: {Identity}, {Receiver}, {MessageId}")]
    private static partial void LogMessageSent(ILogger logger, Guid identity, Peer receiver, MessageId messageId);
}
