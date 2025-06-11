namespace Libplanet.Net.Transports;

public class InvalidMessageTimestampException : Exception
{
    internal InvalidMessageTimestampException(
        string message, DateTimeOffset createdOffset, TimeSpan? buffer, DateTimeOffset currentOffset)
        : base(message)
    {
        CreatedOffset = createdOffset;
        Buffer = buffer;
        CurrentOffset = currentOffset;
    }

    internal DateTimeOffset CreatedOffset { get; }

    internal TimeSpan? Buffer { get; }

    internal DateTimeOffset CurrentOffset { get; }
}
