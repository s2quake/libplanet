namespace Libplanet.Net.Messages;

internal sealed record class GetChainStatusMessage : MessageContent
{
    public override MessageType Type => MessageType.GetChainStatus;

    // public override IEnumerable<byte[]> DataFrames => new byte[][] { };
}
