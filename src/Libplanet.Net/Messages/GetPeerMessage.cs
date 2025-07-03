using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "GetPeerMessage")]
public sealed record class GetPeerMessage : MessageBase
{
    [Property(0)]
    public Address Target { get; init; }
}
