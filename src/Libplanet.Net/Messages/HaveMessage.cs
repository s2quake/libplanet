using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("HaveMessage", Version = 1)]
public sealed partial record class HaveMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<MessageId> Ids { get; init; } = [];
}
