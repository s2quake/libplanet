using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet;

public partial class Blockchain
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "{Name} appended block at height {Height} with hash {BlockHash}")]
    private static partial void LogAppended(ILogger logger, string name, int height, BlockHash blockHash);
}
