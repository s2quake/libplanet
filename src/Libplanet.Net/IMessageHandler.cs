using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal interface IMessageHandler
{
    Type MessageType { get; }

    ValueTask HandleAsync(IReplyContext replyContext, CancellationToken cancellationToken);
}
