namespace Libplanet.Net.Components;

public sealed class PeerNotFoundException : SystemException
{
    public PeerNotFoundException(string message)
        : base(message)
    {
    }

    public PeerNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
