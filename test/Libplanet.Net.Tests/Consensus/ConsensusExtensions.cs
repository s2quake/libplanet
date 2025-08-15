using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Tests.Consensus;

public static class ConsensusExtensions
{
    [Obsolete("not used")]
    public static void ProduceMessage(this Net.Consensus.Consensus @this, ConsensusMessage consensusMessage)
    {
        if (consensusMessage is ConsensusPreVoteMessage preVoteMessage)
        {
            _ = @this.PreVoteAsync(preVoteMessage.PreVote, default);
        }
        else if (consensusMessage is ConsensusPreCommitMessage preCommitMessage)
        {
            _ = @this.PreCommitAsync(preCommitMessage.PreCommit, default);
        }
        else if (consensusMessage is ConsensusProposalMessage proposalMessage)
        {
            _ = @this.ProposeAsync(proposalMessage.Proposal, default);
        }
    }

    [Obsolete("not used")]
    public static async Task WaitUntilAsync(
        this Net.Consensus.Consensus @this, int round, ConsensusStep step, CancellationToken cancellationToken)
    {
        using var resetEvent = new ManualResetEvent(false);
        using var _ = @this.StepChanged.Subscribe(e =>
        {
            if (@this.Round.Index == round && @this.Step == step)
            {
                resetEvent.Set();
            }
        });

        while (!resetEvent.WaitOne(0))
        {
            await Task.Delay(100, cancellationToken);
        }
    }
}
