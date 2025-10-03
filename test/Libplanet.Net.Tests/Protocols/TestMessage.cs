using Libplanet.Net.Messages;
using Libplanet.Serialization;

namespace Libplanet.Net.Tests.Protocols;

[Model("Libplanet_Net_Tests_Protocols_TestMessage", Version = 1)]
internal sealed record class TestMessage : MessageBase
{
    [Property(0)]
    public string Data { get; init; } = string.Empty;
}
