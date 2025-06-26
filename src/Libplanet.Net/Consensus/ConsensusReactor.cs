using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusReactor : IAsyncDisposable
{
    private readonly Gossip _gossip;
    private readonly object _contextLock = new();
    private readonly ConsensusOptions _contextOption;
    private readonly Blockchain _blockchain;
    private readonly ISigner _signer;
    private readonly TimeSpan _newHeightDelay;
    private readonly HashSet<ConsensusMessage> _pendingMessages = [];
    private readonly EvidenceCollector _evidenceCollector = new();
    private readonly IDisposable _tipChangedSubscription;
    private readonly ConcurrentDictionary<Peer, ImmutableHashSet<int>> _peerCatchupRounds = new();
    private readonly IDisposable[] _gossipSubscriptions;

    private int _height;
    private int _round;
    private Consensus _currentConsensus;
    private IDisposable[] _consensusSubscriptions;
    private CancellationTokenSource? _newHeightCts;
    private bool _disposed;

    public ConsensusReactor(ITransport transport, Blockchain blockchain, ConsensusReactorOptions options)
    {
        _gossip = new Gossip(
            transport,
            new GossipOptions
            {
                Seeds = options.SeedPeers,
                Validators = options.ConsensusPeers,
            });

        _gossipSubscriptions =
        [
            _gossip.ValidateReceivedMessage.Subscribe(ValidateMessageToReceive),
            _gossip.ValidateSendingMessage.Subscribe(ValidateMessageToSend),
            _gossip.ProcessMessage.Subscribe(ProcessMessage),
        ];

        _blockchain = blockchain;
        _signer = options.Signer;
        _newHeightDelay = options.TargetBlockInterval;

        _contextOption = options.ContextOptions;
        _currentConsensus = new Consensus(
            _blockchain,
            _blockchain.Tip.Height + 1,
            _signer,
            options: _contextOption);
        _consensusSubscriptions = [.. Subscribe(_currentConsensus)];

        _tipChangedSubscription = _blockchain.TipChanged.Subscribe(OnTipChanged);
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
                _gossip.DenyPeer(peer);
                throw new InvalidOperationException(
                    $"Add {peer} to deny set, since repetitively found higher rounds: " +
                    $"{string.Join(", ", _peerCatchupRounds[peer])}");
            }
        }
    }

    private IEnumerable<IDisposable> Subscribe(Consensus consensus)
    {
        yield return consensus.ExceptionOccurred.Subscribe(exception =>
        {
            if (exception is EvidenceException evidenceException)
            {
                _evidenceCollector.Add(evidenceException);
            }
        });
        yield return consensus.RoundStarted.Subscribe(round =>
        {
            _round = round;
            _gossip.ClearCache();
        });
        yield return consensus.PreVoteed.Subscribe(vote =>
        {
            var message = new ConsensusPreVoteMessage { PreVote = vote };
            _gossip.PublishMessage(message);
        });
        yield return consensus.PreCommitted.Subscribe(vote =>
        {
            var message = new ConsensusPreCommitMessage { PreCommit = vote };
            _gossip.PublishMessage(message);
        });
        yield return consensus.QuorumReached.Subscribe(maj23 =>
        {
            var message = new ConsensusMaj23Message { Maj23 = maj23 };
            _gossip.PublishMessage(message);
        });
        yield return consensus.ProposalClaimed.Subscribe(proposalClaim =>
        {
            var message = new ConsensusProposalClaimMessage { ProposalClaim = proposalClaim };
            _gossip.PublishMessage(message);
        });
        yield return consensus.BlockProposed.Subscribe(proposal =>
        {
            var message = new ConsensusProposalMessage { Proposal = proposal };
            _gossip.PublishMessage(message);
        });
        yield return consensus.Completed.Subscribe(e =>
        {
            var block = e.Block;
            var blockCommit = e.BlockCommit;
            _ = Task.Run(() => _blockchain.Append(block, blockCommit));
        });
    }

    public bool IsRunning { get; private set; }

    public int Height => CurrentContext.Height;

    public int Round => CurrentContext.Round;

    public ImmutableArray<Peer> Validators => _gossip.Peers;

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            Array.ForEach(_gossipSubscriptions, subscription => subscription.Dispose());
            await _gossip.DisposeAsync();
            if (_newHeightCts is not null)
            {
                await _newHeightCts.CancelAsync();
            }

            Array.ForEach(_consensusSubscriptions, subscription => subscription.Dispose());
            _consensusSubscriptions = [];
            await _currentConsensus.DisposeAsync();

            _newHeightCts?.Dispose();
            _newHeightCts = null;
            _tipChangedSubscription.Dispose();
            _disposed = true;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            throw new InvalidOperationException("Consensus reactor is already running.");
        }

        await _gossip.StartAsync(cancellationToken);
        await _currentConsensus.StartAsync(default);
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        await _gossip.StopAsync(cancellationToken);
        IsRunning = false;
    }

    public ConsensusStep Step => CurrentContext.Step;

    internal Consensus CurrentContext
    {
        get
        {
            lock (_contextLock)
            {
                return _currentConsensus;
            }
        }
    }

    public async Task NewHeightAsync(int height, CancellationToken cancellationToken)
    {
        if (height <= Height)
        {
            var message = $"Given new height #{height} must be greater than " +
                          $"the current height #{Height}.";
            throw new ArgumentOutOfRangeException(nameof(height), message);
        }

        Array.ForEach(_consensusSubscriptions, subscription => subscription.Dispose());
        await _currentConsensus.StopAsync(cancellationToken);
        await _currentConsensus.DisposeAsync();
        _currentConsensus = new Consensus(_blockchain, height, _signer, _contextOption);
        _consensusSubscriptions = [.. Subscribe(_currentConsensus)];

        foreach (var message in _pendingMessages)
        {
            if (message.Height == height)
            {
                _currentConsensus.ProduceMessage(message);
            }
        }

        _pendingMessages.RemoveWhere(message => message.Height <= height);
        if (IsRunning)
        {
            await _currentConsensus.StartAsync(default);
            _height = height;
            _peerCatchupRounds.Clear();
            _gossip.ClearDenySet();
        }
    }

    public bool HandleMessage(ConsensusMessage consensusMessage)
    {
        var height = consensusMessage.Height;
        if (height < Height)
        {
            return false;
        }

        lock (_contextLock)
        {
            if (_currentConsensus.Height == height)
            {
                if (consensusMessage is ConsensusPreVoteMessage preVoteMessage)
                {
                    _currentConsensus.PostVote(preVoteMessage.PreVote);
                }
                else if (consensusMessage is ConsensusPreCommitMessage preCommitMessage)
                {
                    _currentConsensus.PostVote(preCommitMessage.PreCommit);
                }
                else if (consensusMessage is ConsensusProposalMessage proposalMessage)
                {
                    _currentConsensus.PostProposal(proposalMessage.Proposal);
                }
            }
            else
            {
                _pendingMessages.Add(consensusMessage);
            }

            return true;
        }
    }

    public VoteSetBits? HandleMaj23(Maj23 maj23)
    {
        int height = maj23.Height;
        if (height < Height)
        {
            // logging
        }
        else
        {
            lock (_contextLock)
            {
                if (_currentConsensus.Height == height)
                {
                    return _currentConsensus.AddMaj23(maj23);
                }
            }
        }

        return null;
    }

    public IEnumerable<ConsensusMessage> HandleVoteSetBits(VoteSetBits voteSetBits)
    {
        int height = voteSetBits.Height;
        if (height < Height)
        {
            // logging
        }
        else
        {
            lock (_contextLock)
            {
                if (_currentConsensus.Height == height)
                {
                    // NOTE: Should check if collected messages have same BlockHash with
                    // VoteSetBit's BlockHash?
                    return _currentConsensus.GetVoteSetBitsResponse(voteSetBits);
                }
            }
        }

        return Array.Empty<ConsensusMessage>();
    }

    public Proposal? HandleProposalClaim(ProposalClaim proposalClaim)
    {
        int height = proposalClaim.Height;
        int round = proposalClaim.Round;
        if (height != Height)
        {
            // logging
        }
        else if (round != Round)
        {
            // logging
        }
        else
        {
            lock (_contextLock)
            {
                if (_currentConsensus.Height == height)
                {
                    // NOTE: Should check if collected messages have same BlockHash with
                    // VoteSetBit's BlockHash?
                    return _currentConsensus.Proposal;
                }
            }
        }

        return null;
    }

    private void OnTipChanged(TipChangedInfo e)
    {
        _newHeightCts?.Cancel();
        _newHeightCts?.Dispose();
        _newHeightCts = new CancellationTokenSource();

        Invoke(_newHeightCts.Token);

        async void Invoke(CancellationToken cancellationToken)
        {
            await Task.Delay(_newHeightDelay, cancellationToken);

            while (_blockchain.GetStateRootHash(e.Tip.Height) == default)
            {
                await Task.Delay(100, cancellationToken);
            }

            try
            {
                AddEvidenceToBlockChain(e.Tip);
                await NewHeightAsync(e.Tip.Height + 1, cancellationToken);
            }
            catch
            {
                // logging
            }
        }
    }

    private void AddEvidenceToBlockChain(Block tip)
    {
        var height = tip.Height;
        var evidenceExceptions
            = _evidenceCollector.Flush().Where(item => item.Height <= height).ToArray();
        foreach (var evidenceException in evidenceExceptions)
        {
            try
            {
                var validators = _blockchain.GetWorld(evidenceException.Height).GetValidators();
                var evidenceContext = new EvidenceContext(validators);
                var evidence = evidenceException.Create(evidenceContext);
                _blockchain.PendingEvidences.Add(evidence);
            }
            catch
            {
                // logging
            }
        }
    }

    private void ProcessMessage(IMessage message)
    {
        switch (message)
        {
            case ConsensusVoteSetBitsMessage voteSetBits:
                // Note: ConsensusVoteSetBitsMsg will not be stored to context's message log.
                var messages = HandleVoteSetBits(voteSetBits.VoteSetBits);
                try
                {
                    var sender = _gossip.Peers.First(
                        peer => peer.Address.Equals(voteSetBits.Validator));
                    _gossip.PublishMessage([sender], [.. messages]);
                }
                catch (InvalidOperationException)
                {
                    // logging
                }

                break;

            case ConsensusMaj23Message maj23Message:
                try
                {
                    VoteSetBits? voteSetBits = HandleMaj23(maj23Message.Maj23);
                    if (voteSetBits is null)
                    {
                        break;
                    }

                    var sender = _gossip.Peers.First(
                        peer => peer.Address.Equals(maj23Message.Validator));
                    _gossip.PublishMessage(
                        [sender],
                        new ConsensusVoteSetBitsMessage { VoteSetBits = voteSetBits });
                }
                catch (InvalidOperationException)
                {
                    // logging
                }

                break;

            case ConsensusProposalClaimMessage proposalClaimmessage:
                try
                {
                    Proposal? proposal = HandleProposalClaim(
                        proposalClaimmessage.ProposalClaim);
                    if (proposal is { } proposalNotNull)
                    {
                        var reply = new ConsensusProposalMessage { Proposal = proposalNotNull };
                        var sender = _gossip.Peers.First(
                            peer => peer.Address.Equals(proposalClaimmessage.Validator));

                        _gossip.PublishMessage([sender], reply);
                    }
                }
                catch (InvalidOperationException)
                {
                    // logging
                }

                break;

            case ConsensusMessage consensusMessage:
                HandleMessage(consensusMessage);
                break;
        }
    }
}
