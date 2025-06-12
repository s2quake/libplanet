using Libplanet.Net.Messages;

namespace Libplanet.Node.Services;

internal sealed class SeedMessageEventArgs(MessageEnvelope message) : EventArgs
{
    public MessageEnvelope Message { get; } = message;
}
