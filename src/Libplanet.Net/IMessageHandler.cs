using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

public interface IMessageHandler
{
    Type MessageType { get; }

    ValueTask HandleAsync(IReplyContext replyContext, CancellationToken cancellationToken);
}
