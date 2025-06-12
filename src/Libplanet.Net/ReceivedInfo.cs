namespace Libplanet.Net;

public sealed record class ReceivedInfo<TItem>(Peer Peer, IEnumerable<TItem> Items)
    where TItem : notnull
{
}
