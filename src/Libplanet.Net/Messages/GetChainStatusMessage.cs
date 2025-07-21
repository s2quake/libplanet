using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "GetChainStatusMessage")]
internal sealed record class GetChainStatusMessage : MessageBase
{
}
