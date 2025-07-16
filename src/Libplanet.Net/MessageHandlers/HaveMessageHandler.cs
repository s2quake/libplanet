using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class HaveMessageHandler(
    ConcurrentDictionary<MessageId, IMessage> messageById,
    ConcurrentDictionary<Peer, HashSet<MessageId>> haveDict)
    : MessageHandlerBase<HaveMessage>
{
    protected override async ValueTask OnHandleAsync(
        HaveMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        await replyContext.PongAsync();
        var ids = message.Ids.Where(id => !messageById.ContainsKey(id)).ToArray();
        if (ids.Length is 0)
        {
            return;
        }

        var peer = replyContext.Sender;
        if (!haveDict.TryGetValue(peer, out HashSet<MessageId>? value))
        {
            value = [];
        }

        foreach (var id in ids)
        {
            value.Add(id);
        }

        haveDict[peer] = value;
    }
}
