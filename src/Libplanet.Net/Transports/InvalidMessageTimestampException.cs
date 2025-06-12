namespace Libplanet.Net.Transports;

public class InvalidMessageTimestampException : Exception
{
    internal InvalidMessageTimestampException(
        DateTimeOffset createdTimestamp, TimeSpan lifetime, DateTimeOffset currentTimestamp)
        : base(GenerateMessage(createdTimestamp, lifetime, currentTimestamp))
    {
        CreatedTimestamp = createdTimestamp;
        Lifetime = lifetime;
        CurrentTimestamp = currentTimestamp;
    }

    internal DateTimeOffset CreatedTimestamp { get; }

    internal TimeSpan Lifetime { get; }

    internal DateTimeOffset CurrentTimestamp { get; }

    private static string GenerateMessage(
        DateTimeOffset createdTimestamp, TimeSpan timestamp, DateTimeOffset currentTimestamp)
    {
        return $"The timestamp of a received message is invalid:\n" +
               $"Message timestamp buffer: {timestamp}\n" +
               $"Current timestamp: " +
               $"{currentTimestamp:o}\n" +
               $"Message timestamp: " +
               $"{createdTimestamp:o}";
    }
}
