namespace Libplanet.Types.Blocks;

public static class DateTimeOffsetExtensions
{
    private static readonly TimeSpan TimestampThreshold = TimeSpan.FromSeconds(15);

    public static void ValidateTimestamp(this DateTimeOffset @this)
        => ValidateTimestamp(@this, DateTimeOffset.UtcNow);

    public static void ValidateTimestamp(this DateTimeOffset @this, DateTimeOffset currentTime)
    {
        if (currentTime + TimestampThreshold < @this)
        {
            var message = $"The block #0's timestamp " +
                $"({@this}) is later than now " +
                $"({currentTime}, threshold: {TimestampThreshold}).";
            throw new InvalidOperationException(message);
            // string hash = metadata is BlockExcerpt h
            //     ? $" {h.Hash}"
            //     : string.Empty;
            // throw new InvalidOperationException(
            //     $"The block #{metadata.Index}{hash}'s timestamp " +
            //     $"({metadata.Timestamp}) is later than now ({currentTime}, " +
            //     $"threshold: {TimestampThreshold}).");
        }
    }
}
