using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "TransactionMessage")]
internal sealed partial record class TransactionMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<byte> Payload { get; init; } = [];
}
