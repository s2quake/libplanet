using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "AggregateMessage")]
public sealed partial record class AggregateMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<IMessage> Messages { get; init; }
}
