namespace Libplanet.Net.Transports;

public class SendMessageFailException : Exception
{
    internal SendMessageFailException(string message, Peer peer)
        : base(message)
    {
        Peer = peer;
    }

    internal SendMessageFailException(string message, Peer peer, Exception innerException)
        : base(message, innerException)
    {
        Peer = peer;
    }

    public Peer Peer { get; }
}
