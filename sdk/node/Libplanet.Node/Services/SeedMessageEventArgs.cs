using Libplanet.Net;
using Libplanet.Net.Messages;

namespace Libplanet.Node.Services;

internal sealed class SeedMessageEventArgs(IReplyContext message) : EventArgs
{
    public IReplyContext Message { get; } = message;
}
