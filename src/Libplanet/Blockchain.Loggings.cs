using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Reactive;
using Libplanet.State;
using Libplanet.Extensions;
using Libplanet.Data;
using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet;

public partial class Blockchain
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "Appended block at height {Height} with hash {BlockHash}")]
    private static partial void LogAppended(ILogger logger, int height, BlockHash blockHash);
}
