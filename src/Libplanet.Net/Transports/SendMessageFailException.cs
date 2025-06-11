namespace Libplanet.Net.Transports;

public class SendMessageFailException : Exception
{
    internal SendMessageFailException(string message, BoundPeer peer)
        : base(message)
    {
        Peer = peer;
    }

    internal SendMessageFailException(string message, BoundPeer peer, Exception innerException)
        : base(message, innerException)
    {
        Peer = peer;
    }

    public BoundPeer Peer { get; }
}
