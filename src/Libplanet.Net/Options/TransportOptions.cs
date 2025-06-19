using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Net.Options;

public sealed record class TransportOptions
{
    public string Host { get; init; } = string.Empty;

    [NonNegative]
    public int Port { get; init; } = 0;
    
    public Protocol Protocol { get; init; } = Protocol.Empty;

    public TimeSpan MessageLifetime { get; init; } = TimeSpan.MaxValue;
}
