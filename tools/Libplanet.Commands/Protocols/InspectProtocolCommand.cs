using JSSoft.Commands;
using Libplanet.Net;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Commands.Protocols;

[CommandSummary("Inspect a protocol.")]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class InspectProtocolCommand(ProtocolCommand protocolCommand)
    : CommandBase(protocolCommand, "inspect")
{
    [CommandPropertyRequired]
    public string ProtocolHex { get; set; } = string.Empty;

    [CommandPropertySwitch]
    public bool Verify { get; set; }

    protected override void OnExecute()
    {
        var protocol = ModelSerializer.Deserialize<Protocol>(ByteUtility.ParseHex(ProtocolHex));
        if (Verify && !protocol.Verify())
        {
            throw new InvalidOperationException("The protocol signature is invalid.");
        }

        FormatProperties.WriteLine(Out, protocol);
    }
}
