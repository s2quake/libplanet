namespace Libplanet.Types.Blocks;

public static class DateTimeOffsetExtensions
{
    private static readonly TimeSpan TimestampThreshold = TimeSpan.FromSeconds(15);

    public static void ValidateTimestamp(this DateTimeOffset @this)
    {
        var currentTime = DateTimeOffset.UtcNow;
        if (currentTime + TimestampThreshold < @this)
        {
            var message = $"The block #0's timestamp " +
                $"({@this}) is later than now " +
                $"({currentTime}, threshold: {TimestampThreshold}).";
            throw new InvalidOperationException(message);
        }
    }
}
