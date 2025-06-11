using Libplanet.Net.Messages;

namespace Libplanet.Net.Transports;

public class CommunicationFailException : Exception
{
    public CommunicationFailException(
        string message, MessageContent.MessageType messageType, BoundPeer peer)
        : base(message)
    {
        Peer = peer;
        MessageType = messageType;
    }

    public CommunicationFailException(
        string message, MessageContent.MessageType messageType, BoundPeer peer, Exception innerException)
        : base(message, innerException)
    {
        Peer = peer;
        MessageType = messageType;
    }

    public BoundPeer Peer { get; }

    public MessageContent.MessageType MessageType { get; }
}
