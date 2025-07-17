using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal abstract class ConsensusMessageHanadlerBase<T>
    : MessageHandlerBase<T>
    where T : ConsensusMessage
{
}
