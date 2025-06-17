using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "WantMessage")]
public sealed partial record class WantMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<MessageId> Ids { get; init; } = [];
}
