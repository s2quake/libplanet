namespace Libplanet.Net;

public sealed class InvalidProtocolException : SystemException
{
    public InvalidProtocolException(string message)
        : base(message)
    {
    }

    public InvalidProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public required ProtocolHash ProtocolHash { get; init; }
}
