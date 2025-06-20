namespace Libplanet.Net.Consensus;

public class InvalidMaj23Exception : Exception
{
    public InvalidMaj23Exception(string message, Maj23 maj23, Exception innerException)
        : base(message, innerException)
    {
        Maj23 = maj23;
    }

    public InvalidMaj23Exception(string message, Maj23 maj23)
        : base(message)
    {
        Maj23 = maj23;
    }

    public Maj23 Maj23 { get; }
}
