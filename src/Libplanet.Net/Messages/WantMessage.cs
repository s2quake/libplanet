using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "WantMessage")]
public sealed partial record class WantMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<MessageId> Ids { get; init; } = [];

    public override MessageType Type => MessageType.WantMessage;
}
