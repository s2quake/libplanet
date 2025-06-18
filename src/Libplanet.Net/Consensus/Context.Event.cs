using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public partial class Context
{
    internal event EventHandler<int>? HeightStarted;

    internal event EventHandler<int>? RoundStarted;

    internal event EventHandler<ConsensusMessage>? MessageToPublish;

    internal event EventHandler<Exception>? ExceptionOccurred;

    internal event EventHandler<(int Round, ConsensusStep Step)>? TimeoutProcessed;

    internal event EventHandler<ContextState>? StateChanged;

    internal event EventHandler<ConsensusMessage>? MessageConsumed;

    internal event EventHandler<System.Action>? MutationConsumed;

    internal event EventHandler<(int Round, VoteFlag Flag, IEnumerable<Vote> Votes)>? VoteSetModified;
}
