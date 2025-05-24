using System.Text;
using Libplanet.Net.Messages;
using Libplanet.Serialization;

namespace Libplanet.Net.Tests.Protocols;

[Model(Version = 1)]
internal sealed record class TestMessage : MessageContent
{
    [Property(0)]
    public string Data { get; init; } = string.Empty;

    public override MessageType Type => MessageType.Ping;
}
