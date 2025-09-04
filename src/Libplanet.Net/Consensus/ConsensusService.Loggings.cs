using Microsoft.Extensions.Logging;

namespace Libplanet.Net.Consensus;

public sealed partial class ConsensusService
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "{Name} NewHeight begin: {Height}")]
    private static partial void LogNewHeightBegin(ILogger logger, string name, int height);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "{Name} NewHeight end: {Height}")]
    private static partial void LogNewHeightEnd(ILogger logger, string name, int height);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "{Name} Evidence occurred: {EvidenceType} at height {Height}")]
    private static partial void LogEvidenceOccurred(ILogger logger, string name, string evidenceType, int height);
}
