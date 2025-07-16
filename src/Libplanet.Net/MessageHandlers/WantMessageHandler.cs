using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class WantMessageHandler(
    ConcurrentDictionary<MessageId, IMessage> messageById,
    ConcurrentDictionary<Peer, HashSet<MessageId>> haveDict)
    : MessageHandlerBase<HaveMessage>
{
    protected override async ValueTask OnHandleAsync(
        HaveMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        var wantMessage = (WantMessage)replyContext.Message;
        var messages = wantMessage.Ids.Select(id => messageById[id]).ToArray();

        Parallel.ForEach(messages, Invoke);

        void Invoke(IMessage message)
        {
            try
            {
                // _validateSendingMessageSubject.OnNext(message);
                _ = replyContext.NextAsync(message);
            }
            catch (Exception)
            {
                // do nothing
            }
        }
    }
}
