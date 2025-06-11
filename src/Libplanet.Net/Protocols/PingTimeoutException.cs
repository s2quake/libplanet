namespace Libplanet.Net.Protocols;

public class PingTimeoutException : TimeoutException
{
    public PingTimeoutException(Peer target)
        : base()
    {
        Target = target;
    }

    public PingTimeoutException(string message, Peer target)
        : base(message)
    {
        Target = target;
    }

    public PingTimeoutException(string message, Peer target, Exception innerException)
        : base(message, innerException)
    {
        Target = target;
    }

    public Peer Target { get; }
}
