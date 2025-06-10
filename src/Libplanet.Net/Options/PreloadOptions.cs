namespace Libplanet.Net.Options;

public sealed record class PreloadOptions
{
    public const int DefaultDialTimeout = 5;

    public const long DefaultTipDeltaThreshold = 25L;

    public TimeSpan DialTimeout { get; set; } = TimeSpan.FromSeconds(DefaultDialTimeout);

    public long TipDeltaThreshold { get; set; } = DefaultTipDeltaThreshold;
}
