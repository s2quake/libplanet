namespace Libplanet.Net.Options;

public sealed record class TimeoutOptions
{
    public const int DefaultMaxTimeout = 150;
    public const int DefaultDialTimeout = 1;
    public const int DefaultGetBlockHashesTimeout = 30;
    public const int DefaultGetBlocksBaseTimeout = 15;
    public const int DefaultGetBlocksPerBlockHashTimeout = 1;
    public const int DefaultGetTxsBaseTimeout = 3;
    public const int DefaultGetTxsPerTxIdTimeout = 1;

    public TimeSpan MaxTimeout { get; init; } = TimeSpan.FromSeconds(DefaultMaxTimeout);

    public TimeSpan DialTimeout { get; init; } = TimeSpan.FromSeconds(DefaultDialTimeout);

    public TimeSpan GetBlockHashesTimeout { get; init; } = TimeSpan.FromSeconds(DefaultGetBlockHashesTimeout);

    public TimeSpan GetBlocksBaseTimeout { get; init; } = TimeSpan.FromSeconds(DefaultGetBlocksBaseTimeout);

    public TimeSpan GetBlocksPerBlockHashTimeout { get; init; } = TimeSpan.FromSeconds(DefaultGetBlocksPerBlockHashTimeout);

    public TimeSpan GetTxsBaseTimeout { get; init; } = TimeSpan.FromSeconds(DefaultGetTxsBaseTimeout);

    public TimeSpan GetTxsPerTxIdTimeout { get; init; } = TimeSpan.FromSeconds(DefaultGetTxsPerTxIdTimeout);
}
