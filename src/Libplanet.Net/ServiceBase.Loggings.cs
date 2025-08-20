using Microsoft.Extensions.Logging;

namespace Libplanet.Net;

public abstract partial class ServiceBase
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "{Name} Started")]
    private static partial void LogStarted(ILogger logger, string name);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "{Name} Stopped")]
    private static partial void LogStopped(ILogger logger, string name);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "{Name} Disposed")]
    private static partial void LogDisposed(ILogger logger, string name);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "{Name} Recovered")]
    private static partial void LogRecovered(ILogger logger, string name);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error, Message = "{Name} Start failed")]
    private static partial void LogStartFailed(ILogger logger, string name, Exception exception);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "{Name} Stop failed")]
    private static partial void LogStopFailed(ILogger logger, string name, Exception exception);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Error, Message = "{Name} Recover failed")]
    private static partial void LogRecoverFailed(ILogger logger, string name, Exception exception);
}
