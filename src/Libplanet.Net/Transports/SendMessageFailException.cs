using Libplanet.Net.Messages;

namespace Libplanet.Net.Transports;

public class SendMessageFailException : Exception
{
    internal SendMessageFailException(Peer peer, IMessage message)
        : base(GenerateMessage(peer, message))
    {
        Message = message;
        Peer = peer;
    }

    public Peer Peer { get; }

    public new IMessage Message { get; }

    private static string GenerateMessage(Peer peer, IMessage message)
    {
        return $"Failed to send {message} to {peer}.";
    }
}
