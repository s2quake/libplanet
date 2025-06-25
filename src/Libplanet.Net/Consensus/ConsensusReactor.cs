using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.State;
using Libplanet.Types;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusReactor : IAsyncDisposable
{
    private readonly Gossip _gossip;
    private readonly object _contextLock = new();
    private readonly ConsensusOptions _contextOption;
    private readonly Blockchain _blockchain;
    private readonly PrivateKey _privateKey;
    private readonly TimeSpan _newHeightDelay;
    private readonly HashSet<ConsensusMessage> _pendingMessages = [];
    private readonly EvidenceExceptionCollector _evidenceCollector = new();
    private readonly IDisposable _tipChangedSubscription;
    private readonly ConcurrentDictionary<Peer, ImmutableHashSet<int>> _peerCatchupRounds = new();
    private readonly List<IDisposable> _subscriptionList;

    private int _height;
    private int _round;
    private Consensus _currentConsensus;
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

        _subscriptionList =
        [
            _gossip.ValidateReceivedMessage.Subscribe(ValidateMessageToReceive),
            _gossip.ValidateSendingMessage.Subscribe(ValidateMessageToSend),
            _gossip.ProcessMessage.Subscribe(ProcessMessage),
        ];

        _blockchain = blockchain;
        _privateKey = options.PrivateKey;
        _newHeightDelay = options.TargetBlockInterval;

        _contextOption = options.ContextOptions;
        _currentConsensus = new Consensus(
            _blockchain,
            _blockchain.Tip.Height + 1,
            _privateKey.AsSigner(),
            options: _contextOption);
        AttachEventHandlers(_currentConsensus);

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


    // public event EventHandler<(int Height, ConsensusMessage Message)>? MessagePublished;

    internal event EventHandler<(int Height, Exception)>? ExceptionOccurred;

    // internal event EventHandler<ConsensusState>? StateChanged;

    internal event EventHandler<(int Height, ConsensusMessage Message)>? MessageConsumed;

    private void AttachEventHandlers(Consensus consensus)
    {
        // NOTE: Events for testing and debugging.
        consensus.ExceptionOccurred.Subscribe(exception => ExceptionOccurred?.Invoke(this, (consensus.Height, exception)));
        // context.TimeoutProcessed += (sender, eventArgs) =>
        //     TimeoutProcessed?.Invoke(this, (context.Height, eventArgs.Round, eventArgs.Step));
        // consensus.StateChanged.Subscribe(state => StateChanged?.Invoke(this, state));
        // context.MessageConsumed += (sender, message) =>
        //     MessageConsumed?.Invoke(this, (context.Height, message));
        // context.MutationConsumed += (sender, action) =>
        //     MutationConsumed?.Invoke(this, (context.Height, action));

        // NOTE: Events for consensus logic.
        consensus.Started.Subscribe(height =>
        {
            _height = height;
            _peerCatchupRounds.Clear();
            _gossip.ClearDenySet();
        });
        consensus.RoundStarted.Subscribe(round =>
        {
            _round = round;
            _gossip.ClearCache();
        });
        // consensus.MessagePublished.Subscribe(message =>
        // {
        //     _gossip.PublishMessage(message);
        //     MessagePublished?.Invoke(this, (consensus.Height, message));
        // });
        consensus.PreVoteEntered.Subscribe(blockHash =>
        {
            var round = consensus.Round;
            var message = new ConsensusPreVoteMessage
            {
                PreVote = consensus.CreateVote(round, blockHash, VoteType.PreVote),
            };
            _gossip.PublishMessage(message);
        });
        consensus.PreCommitEntered.Subscribe(blockHash =>
        {
            var round = consensus.Round;
            var message = new ConsensusPreCommitMessage
            {
                PreCommit = consensus.CreateVote(round, blockHash, VoteType.PreCommit),
            };
            _gossip.PublishMessage(message);
        });
        consensus.Maj23Achieved.Subscribe(e =>
        {
            var round = consensus.Round;
            var blockHash = e.BlockHash;
            var voteType = e.VoteType;
            var maj23 = consensus.CreateMaj23(round, blockHash, voteType);
            var message = new ConsensusMaj23Message
            {
                Maj23 = maj23,
            };
            _gossip.PublishMessage(message);
        });
        consensus.ProposalClaimed.Subscribe(blockHash =>
        {
            var proposalClaim = new ProposalClaimMetadata
            {
                Height = consensus.Height,
                Round = consensus.Round,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = _privateKey.Address,
            }.Sign(_privateKey.AsSigner());
            var message = new ConsensusProposalClaimMessage
            {
                ProposalClaim = proposalClaim,
            };
            _gossip.PublishMessage(message);
        });
        consensus.BlockProposed.Subscribe(e =>
        {
            var proposal = new ProposalMetadata
            {
                Height = Height,
                Round = Round,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _privateKey.Address,
                ValidRound = e.ValidRound,
            }.Sign(_privateKey.AsSigner(), e.Block);
            var message = new ConsensusProposalMessage { Proposal = proposal };
            _gossip.PublishMessage(message);
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
            _subscriptionList.ForEach(subscription => subscription.Dispose());
            await _gossip.DisposeAsync();
            if (_newHeightCts is not null)
            {
                await _newHeightCts.CancelAsync();
            }

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
            throw new InvalidHeightIncreasingException(
                $"Given new height #{height} must be greater than " +
                $"the current height #{Height}.");
        }

        var lastCommit = BlockCommit.Empty;
        if (_currentConsensus.Height == height - 1 &&
            _currentConsensus.GetBlockCommit() is { } prevCommit)
        {
            lastCommit = prevCommit;
        }

        if (lastCommit == default &&
            _blockchain.BlockCommits[height - 1] is { } storedCommit)
        {
            lastCommit = storedCommit;
        }

        await _currentConsensus.DisposeAsync();
        _currentConsensus = new Consensus(
            _blockchain,
            height,
            _privateKey.AsSigner(),
            options: _contextOption);
        AttachEventHandlers(_currentConsensus);

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
                _currentConsensus.ProduceMessage(consensusMessage);
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
                HandleEvidenceExceptions();
                AddEvidenceToBlockChain(e.Tip);
                await NewHeightAsync(e.Tip.Height + 1, cancellationToken);
            }
            catch
            {
                // logging
            }
        }
    }

    private void HandleEvidenceExceptions()
    {
        var evidenceExceptions = _currentConsensus.CollectEvidenceExceptions();
        _evidenceCollector.AddRange(evidenceExceptions);
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
