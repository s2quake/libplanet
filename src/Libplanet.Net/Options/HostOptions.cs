using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Net.Options;

public sealed record class HostOptions
{
    public string Host { get; init; } = string.Empty;

    [NonNegative]
    public int Port { get; init; } = 0;
}
