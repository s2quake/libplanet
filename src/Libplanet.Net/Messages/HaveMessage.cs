using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "HaveMessage")]
public sealed partial record class HaveMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<MessageId> Ids { get; init; } = [];
}
