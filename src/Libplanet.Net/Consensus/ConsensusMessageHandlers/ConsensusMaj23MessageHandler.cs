using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;

namespace Libplanet.Net.Consensus.ConsensusMessageHandlers;

internal sealed class ConsensusMaj23MessageHandler(ConsensusReactor consensusReactor, Gossip gossip)
    : MessageHandlerBase<ConsensusMaj23Message>
{
    protected override async ValueTask OnHandleAsync(
        ConsensusMaj23Message message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        VoteSetBits? voteSetBits = consensusReactor.HandleMaj23(message.Maj23);
        if (voteSetBits is null)
        {
            return;
        }

        var sender = gossip.Peers.First(peer => peer.Address.Equals(message.Validator));
        gossip.PublishMessage(
            [sender],
            new ConsensusVoteSetBitsMessage { VoteSetBits = voteSetBits });

        await ValueTask.CompletedTask;
    }
}
