namespace Libplanet.Net.Messages;

internal sealed record class BlocksMessage : MessageContent
{
    public ImmutableArray<byte[]> Payloads { get; init; }

    public override MessageType Type => MessageType.Blocks;
}
