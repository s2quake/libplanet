using Libplanet.Types;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Components;

public static class PeerExplorerExtensions
{
    public static (ImmutableArray<Peer>, IMessage) BroadcastBlock(this PeerExplorer @this, Blockchain blockchain)
        => BroadcastBlock(@this, blockchain, except: default);

    public static (ImmutableArray<Peer>, IMessage) BroadcastBlock(
        this PeerExplorer @this, Blockchain blockchain, ImmutableArray<Peer> except)
        => BroadcastBlock(@this, blockchain, blockchain.Tip, except);

    public static (ImmutableArray<Peer>, IMessage) BroadcastBlock(
        this PeerExplorer @this, Blockchain blockchain, Block block)
        => BroadcastBlock(@this, blockchain, block, default);

    public static (ImmutableArray<Peer>, IMessage) BroadcastBlock(
        this PeerExplorer @this, Blockchain blockchain, Block block, ImmutableArray<Peer> except)
    {
        var message = new BlockSummaryMessage
        {
            GenesisBlockHash = blockchain.Genesis.BlockHash,
            BlockSummary = block,
        };
        return (@this.Broadcast(message, except), message);
    }

    public static (ImmutableArray<Peer>, IMessage) Broadcast(this PeerExplorer @this, BlockHash genesisHash, Block block)
    {
        var message = new BlockSummaryMessage
        {
            GenesisBlockHash = genesisHash,
            BlockSummary = block,
        };
        return (@this.Broadcast(message), message);
    }

    public static (ImmutableArray<Peer>, IMessage) BroadcastTransaction(
        this PeerExplorer @this, ImmutableArray<Transaction> transactions)
        => BroadcastTransaction(@this, transactions, default);

    public static (ImmutableArray<Peer>, IMessage) BroadcastTransaction(
        this PeerExplorer @this, ImmutableArray<Transaction> transactions, ImmutableArray<Peer> except)
        => BroadcastTransaction(@this, [.. transactions.Select(tx => tx.Id)], except);

    public static (ImmutableArray<Peer>, IMessage) BroadcastTransaction(
        this PeerExplorer @this, ImmutableArray<TxId> txIds)
        => BroadcastTransaction(@this, txIds, default);

    public static (ImmutableArray<Peer>, IMessage) BroadcastTransaction(
        this PeerExplorer @this, ImmutableArray<TxId> txIds, ImmutableArray<Peer> except)
    {
        var message = new TxIdMessage
        {
            Ids = txIds,
        };
        return (@this.Broadcast(message, except), message);
    }

    public static (ImmutableArray<Peer>, IMessage) BroadcastEvidence(
        this PeerExplorer @this, ImmutableArray<EvidenceBase> evidence)
        => BroadcastEvidence(@this, evidence, default);

    public static (ImmutableArray<Peer>, IMessage) BroadcastEvidence(
        this PeerExplorer @this, ImmutableArray<EvidenceBase> evidence, ImmutableArray<Peer> except)
        => BroadcastEvidence(@this, [.. evidence.Select(tx => tx.Id)], except);

    public static (ImmutableArray<Peer>, IMessage) BroadcastEvidence(
        this PeerExplorer @this, ImmutableArray<EvidenceId> evidenceIds)
        => BroadcastEvidence(@this, evidenceIds, default);

    public static (ImmutableArray<Peer>, IMessage) BroadcastEvidence(
        this PeerExplorer @this, ImmutableArray<EvidenceId> evidenceIds, ImmutableArray<Peer> except)
    {
        var message = new EvidenceIdMessage
        {
            Ids = evidenceIds,
        };
        return (@this.Broadcast(message, except), message);
    }

    public static (ImmutableArray<Peer>, IMessage) BroadcastMessages(
        this PeerExplorer @this, ImmutableArray<IMessage> messages)
        => BroadcastMessages(@this, messages, default);

    public static (ImmutableArray<Peer>, IMessage) BroadcastMessages(
        this PeerExplorer @this, ImmutableArray<IMessage> messages, ImmutableArray<Peer> except)
        => BroadcastMessages(@this, [.. messages.Select(tx => tx.Id)], except);

    public static (ImmutableArray<Peer>, IMessage) BroadcastMessages(
        this PeerExplorer @this, ImmutableArray<MessageId> messagesIds)
        => BroadcastMessages(@this, messagesIds, default);

    public static (ImmutableArray<Peer>, IMessage) BroadcastMessages(
        this PeerExplorer @this, ImmutableArray<MessageId> messagesIds, ImmutableArray<Peer> except)
    {
        var message = new HaveMessage
        {
            Ids = messagesIds,
        };
        return (@this.Broadcast(message, except), message);
    }
}
