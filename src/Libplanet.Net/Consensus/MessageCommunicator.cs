using System.Collections.Concurrent;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus;

public sealed class MessageCommunicator
{
    private int _height;
    private int _round;
    private readonly ConcurrentDictionary<Peer, ImmutableHashSet<int>> _peerCatchupRounds;

    public MessageCommunicator(
        ITransport transport,
        ImmutableArray<Peer> validators,
        ImmutableArray<Peer> seeds,
        Action<IMessage> processMessage)
    {
        Gossip = new Gossip(
            transport,
            validators,
            seeds,
            ValidateMessageToReceive,
            ValidateMessageToSend,
            processMessage);
        _peerCatchupRounds = new ConcurrentDictionary<Peer, ImmutableHashSet<int>>();
    }

    internal Gossip Gossip { get; }

    public void PublishMessage(ConsensusMessage message) => Gossip.PublishMessage(message);

    public void StartHeight(int height)
    {
        _height = height;
        _peerCatchupRounds.Clear();
        Gossip.ClearDenySet();
    }

    public void StartRound(int round)
    {
        _round = round;
        Gossip.ClearCache();
    }

    private void ValidateMessageToReceive(MessageEnvelope message)
    {
        if (message.Message is ConsensusVoteMessage voteMsg)
        {
            FilterDifferentHeightVote(voteMsg);
            FilterHigherRoundVoteSpam(voteMsg, message.Peer);
        }
    }

    private void ValidateMessageToSend(IMessage message)
    {
        if (message is ConsensusVoteMessage voteMsg)
        {
            if (voteMsg.Height != _height)
            {
                throw new InvalidOperationException(
                    $"Cannot send vote of height different from context's");
            }

            if (voteMsg.Round > _round)
            {
                throw new InvalidOperationException(
                    $"Cannot send vote of round higher than context's");
            }
        }
    }

    private void FilterDifferentHeightVote(ConsensusVoteMessage voteMsg)
    {
        if (voteMsg.Height != _height)
        {
            throw new InvalidOperationException(
                $"Filtered vote from different height: {voteMsg.Height}");
        }
    }

    private void FilterHigherRoundVoteSpam(ConsensusVoteMessage voteMsg, Peer peer)
    {
        if (voteMsg.Height == _height &&
            voteMsg.Round > _round)
        {
            _peerCatchupRounds.AddOrUpdate(
                peer,
                [voteMsg.Round],
                (peer, set) => set.Add(voteMsg.Round));

            if (_peerCatchupRounds.TryGetValue(peer, out var set) && set.Count > 2)
            {
                Gossip.DenyPeer(peer);
                throw new InvalidOperationException(
                    $"Add {peer} to deny set, since repetitively found higher rounds: " +
                    $"{string.Join(", ", _peerCatchupRounds[peer])}");
            }
        }
    }
}
