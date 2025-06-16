namespace Libplanet.Net.Options;

public sealed record class ProtocolOptions
{
    public Protocol Protocol { get; init; } = Protocol.Empty;

    public TimeSpan MessageLifetime { get; init; } = TimeSpan.MaxValue;
}
