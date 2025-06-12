namespace Libplanet.Net.Messages;

public sealed record class DifferentVersionMessage : MessageBase
{
    public override MessageType Type => MessageType.DifferentVersion;
}
