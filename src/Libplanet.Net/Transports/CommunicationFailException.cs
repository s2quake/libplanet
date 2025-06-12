using Libplanet.Net.Messages;

namespace Libplanet.Net.Transports;

public class CommunicationFailException : Exception
{
    public CommunicationFailException(
        string message, Type messageType, Peer peer)
        : base(message)
    {
        Peer = peer;
        MessageType = messageType;
    }

    public CommunicationFailException(
        string message, Type messageType, Peer peer, Exception innerException)
        : base(message, innerException)
    {
        Peer = peer;
        MessageType = messageType;
    }

    public Peer Peer { get; }

    public Type MessageType { get; }
}
