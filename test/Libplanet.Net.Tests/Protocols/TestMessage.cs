using Libplanet.Net.Messages;
using Libplanet.Serialization;

namespace Libplanet.Net.Tests.Protocols;

[Model(Version = 1, TypeName = "Libplanet_Net_Tests_Protocols_TestMessage")]
internal sealed record class TestMessage : MessageBase
{
    [Property(0)]
    public string Data { get; init; } = string.Empty;

    public override MessageType Type => MessageType.Ping;
}
