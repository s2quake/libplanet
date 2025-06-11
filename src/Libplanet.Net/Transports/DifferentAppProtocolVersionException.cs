namespace Libplanet.Net.Transports;

public class DifferentAppProtocolVersionException(
    string message, ProtocolVersion expectedAppProtocolVersion, ProtocolVersion actualAppProtocolVersion, bool trusted)
    : Exception(message)
{
    public ProtocolVersion ExpectedApv { get; } = expectedAppProtocolVersion;

    public ProtocolVersion ActualApv { get; } = actualAppProtocolVersion;

    public bool Trusted { get; } = trusted;
}
