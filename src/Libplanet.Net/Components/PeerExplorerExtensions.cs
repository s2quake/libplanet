using Libplanet.Types;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Components;

public static class PeerExplorerExtensions
{
    public static (ImmutableArray<Peer>, IMessage) Broadcast(this PeerExplorer @this, BlockHash genesisHash, Block block)
    {
        var message = new BlockSummaryMessage
        {
            GenesisHash = genesisHash,
            BlockSummary = block,
        };
        return (@this.Broadcast(message), message);
    }

    public static (ImmutableArray<Peer>, IMessage) Broadcast(
        this PeerExplorer @this, ImmutableArray<Transaction> transactions)
        => Broadcast(@this, transactions, default);

    public static (ImmutableArray<Peer>, IMessage) Broadcast(
        this PeerExplorer @this, ImmutableArray<Transaction> transactions, BroadcastOptions options)
        => Broadcast(@this, [.. transactions.Select(tx => tx.Id)], options);

    public static (ImmutableArray<Peer>, IMessage) Broadcast(
        this PeerExplorer @this, ImmutableArray<TxId> txIds)
        => Broadcast(@this, txIds, default);

    public static (ImmutableArray<Peer>, IMessage) Broadcast(
        this PeerExplorer @this, ImmutableArray<TxId> txIds, BroadcastOptions options)
    {
        var message = new TxIdMessage
        {
            Ids = txIds,
        };
        return (@this.Broadcast(message, options), message);
    }
}
