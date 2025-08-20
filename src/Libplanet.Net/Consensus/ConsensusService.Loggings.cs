using Microsoft.Extensions.Logging;

namespace Libplanet.Net.Consensus;

public sealed partial class ConsensusService
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "NewHeight begin: {Height}")]
    private static partial void LogNewHeightBegin(ILogger logger, int height);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "NewHeight end: {Height}")]
    private static partial void LogNewHeightEnd(ILogger logger, int height);
}
