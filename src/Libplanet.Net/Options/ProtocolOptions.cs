namespace Libplanet.Net.Options;

public sealed record class ProtocolOptions
{
    public Protocol Protocol { get; init; }

    public TimeSpan MessageLifetime { get; init; } = TimeSpan.Zero;
}
