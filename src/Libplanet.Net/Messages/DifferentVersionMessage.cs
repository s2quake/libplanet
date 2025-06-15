namespace Libplanet.Net.Messages;

[Obsolete]
public sealed record class DifferentVersionMessage : MessageBase
{
    public override MessageType Type => MessageType.DifferentVersion;
}
