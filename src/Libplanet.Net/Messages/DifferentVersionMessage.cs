namespace Libplanet.Net.Messages;

public sealed record class DifferentVersionMessage : MessageContent
{
    public override MessageType Type => MessageType.DifferentVersion;
}
