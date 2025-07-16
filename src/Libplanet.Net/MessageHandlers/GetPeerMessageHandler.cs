using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class GetPeerMessageHandler(Address owner, RoutingTable table)
    : MessageHandlerBase<GetPeerMessage>
{
    protected override async ValueTask OnHandleAsync(
        GetPeerMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        if (replyContext.Sender.Address.Equals(owner))
        {
            throw new InvalidOperationException("Cannot receive ping from self.");
        }

        var target = message.Target;
        var k = RoutingTable.BucketCount;
        var peers = table.GetNeighbors(target, k, includeTarget: true);
        var peerMessage = new PeerMessage { Peers = [.. peers] };
        await replyContext.CompleteAsync(peerMessage);
    }
}
