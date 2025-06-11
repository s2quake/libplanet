namespace Libplanet.Net.Transports;

public class DifferentAppProtocolVersionException(
    string message, Protocol expectedAppProtocolVersion, Protocol actualAppProtocolVersion, bool trusted)
    : Exception(message)
{
    public Protocol ExpectedApv { get; } = expectedAppProtocolVersion;

    public Protocol ActualApv { get; } = actualAppProtocolVersion;

    public bool Trusted { get; } = trusted;
}
