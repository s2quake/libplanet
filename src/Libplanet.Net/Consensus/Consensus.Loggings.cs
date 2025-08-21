using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet.Net.Consensus;

public sealed partial class Consensus
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "Proposed: {Height}, {Round}, {Validator}")]
    private static partial void LogProposed(ILogger logger, int height, int round, Address validator);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "PreVoted: {Height}, {Round}, {Validator}")]
    private static partial void LogPreVoted(ILogger logger, int height, int round, Address validator);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "PreCommitted: {Height}, {Round}, {Validator}")]
    private static partial void LogPreCommitted(ILogger logger, int height, int round, Address validator);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "RoundStarted: {Height}, {Round}")]
    private static partial void LogRoundStarted(ILogger logger, int height, int round);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "StepChanged: {Height}, {Round}, {Step}, {BlockHash}")]
    private static partial void LogStepChanged(ILogger logger, int height, int round, ConsensusStep step, BlockHash blockHash);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Debug, Message = "TimeoutOccurred: {Height}, {Round}, {Step}")]
    private static partial void LogTimeoutOccurred(ILogger logger, int height, int round, ConsensusStep step);
}
