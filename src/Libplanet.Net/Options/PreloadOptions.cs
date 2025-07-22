namespace Libplanet.Net.Options;

public sealed record class PreloadOptions
{
    public bool Enabled { get; init; } = true;

    public TimeSpan DialTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public int TipDeltaThreshold { get; init; } = 25;
}
