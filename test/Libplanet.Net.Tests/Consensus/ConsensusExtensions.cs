using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Tests.Consensus;

public static class ConsensusExtensions
{
    public static void ProduceMessage(this Net.Consensus.Consensus @this, ConsensusMessage consensusMessage)
    {
        if (consensusMessage is ConsensusPreVoteMessage preVoteMessage)
        {
            @this.Post(preVoteMessage.PreVote);
        }
        else if (consensusMessage is ConsensusPreCommitMessage preCommitMessage)
        {
            @this.Post(preCommitMessage.PreCommit);
        }
        else if (consensusMessage is ConsensusProposalMessage proposalMessage)
        {
            @this.Post(proposalMessage.Proposal);
        }
    }

    public static async Task WaitUntilAsync(
        this Net.Consensus.Consensus @this, int round, ConsensusStep step, CancellationToken cancellationToken)
    {
        using var resetEvent = new ManualResetEvent(false);
        using var _ = @this.StepChanged.Subscribe(e =>
        {
            if (@this.Round == round && @this.Step == step)
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
