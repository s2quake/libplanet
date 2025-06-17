using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "GetBlockHashesMessage")]
internal sealed record class GetBlockHashesMessage : MessageBase
{
    [Property(0)]
    public required BlockHash BlockHash { get; init; }
}
