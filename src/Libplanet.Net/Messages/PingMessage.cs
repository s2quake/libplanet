using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("PingMessage", Version = 1)]
public sealed record class PingMessage : MessageBase
{
}
