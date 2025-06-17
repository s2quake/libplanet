namespace Libplanet.Net.Messages;

internal sealed record class BlocksMessage : MessageBase
{
    public ImmutableArray<byte[]> Payloads { get; init; }
}
