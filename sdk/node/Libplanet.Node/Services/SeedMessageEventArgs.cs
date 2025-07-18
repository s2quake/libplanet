using Libplanet.Net;
using Libplanet.Net.Messages;

namespace Libplanet.Node.Services;

internal sealed class SeedMessageEventArgs(object message) : EventArgs
{
    public object Message { get; } = message;
}
