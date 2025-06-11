namespace Libplanet.Net.Transports;

public class InvalidProtocolException(string message, Protocol expectedProtocol, Protocol actualProtocol, bool trusted)
    : Exception(message)
{
    public Protocol ExpectedProtocol { get; } = expectedProtocol;

    public Protocol ActualProtocol { get; } = actualProtocol;

    public bool Trusted { get; } = trusted;
}
