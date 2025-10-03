using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("WantMessage", Version = 1)]
public sealed partial record class WantMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<MessageId> Ids { get; init; } = [];
}
