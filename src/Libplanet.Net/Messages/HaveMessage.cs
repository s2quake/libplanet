using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class HaveMessage : MessageContent, IEquatable<HaveMessage>
{
    [Property(0)]
    public ImmutableArray<MessageId> Ids { get; init; } = [];

    public override MessageType Type => MessageType.HaveMessage;

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
    
    public bool Equals(HaveMessage? other) => ModelResolver.Equals(this, other);
}
