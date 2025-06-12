namespace Libplanet.Net.Messages;

internal sealed record class GetChainStatusMessage : MessageBase
{
    public override MessageType Type => MessageType.GetChainStatus;
}
