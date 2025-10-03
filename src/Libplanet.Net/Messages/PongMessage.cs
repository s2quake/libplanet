using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("PongMessage", Version = 1)]
public sealed partial record class PongMessage : MessageBase
{
}
