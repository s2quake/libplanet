using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;
using Libplanet.State;
using Libplanet.Types;
using Libplanet.Net.Components;
using Libplanet.Extensions;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusService : ServiceBase
{
    private readonly Subject<int> _heightChangedSubject = new();
    private readonly Subject<Round> _roundChangedSubject = new();
    private readonly Subject<ConsensusStep> _stepChangedSubject = new();
    private readonly Subject<ConsensusStep> _timeoutOccurredSubject = new();

    private readonly Subject<Proposal> _blockProposedSubject = new();
    private readonly ITransport _transport;
    private readonly PeerCollection _peers;
    private readonly PeerExplorer _peerExplorer;
    private readonly Gossip _gossip;
    private readonly ConsensusOptions _consensusOption;
    private readonly Blockchain _blockchain;
    private readonly ISigner _signer;
    private readonly TimeSpan _newHeightDelay;
    private readonly HashSet<ConsensusMessage> _pendingMessages = [];
    private readonly EvidenceCollector _evidenceCollector = new();
    private readonly ConcurrentDictionary<Peer, ImmutableHashSet<int>> _peerCatchupRounds = new();
    private readonly IDisposable[] _initialiSubscriptions;

    private Dispatcher? _dispatcher;
    private Consensus _consensus;
    private ConsensusController _controller;
    private ConsensusBroadcaster _broadcaster;
    private ConsensusBroadcastingResponder _broadcastingResponder;
    private IDisposable[] _runningSubscriptions;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTimeOffset _tipChangedTime;

    public ConsensusService(
        ISigner signer, Blockchain blockchain, ITransport transport, ConsensusServiceOptions options)
    {
        _signer = signer;
        _transport = transport;
        _peers = new PeerCollection(_transport.Peer.Address);
        _peers.AddMany([.. options.Validators]);
        _peerExplorer = new PeerExplorer(_transport, _peers)
        {
            SeedPeers = options.Seeds,
        };
        _gossip = new Gossip(_transport, _peerExplorer.Peers);

        _blockchain = blockchain;
        _newHeightDelay = options.TargetBlockInterval;
        _consensusOption = options.ConsensusOptions with
        {
            BlockValidators = options.ConsensusOptions.BlockValidators.Add(new RelayObjectValidator<Block>(b => _blockchain.Validate(b)))
        };
        Height = _blockchain.Tip.Height + 1;
        _consensus = new Consensus(Height, _blockchain.GetValidators(Height), _consensusOption);
        _controller = new ConsensusController(_signer, _consensus, _blockchain);
        _broadcaster = new ConsensusBroadcaster(_controller, _gossip);
        _broadcastingResponder = new ConsensusBroadcastingResponder(_signer, _consensus, _gossip);

        _runningSubscriptions =
        [
            // .. Subscribe(_controller),
            .. Subscribe(_consensus),
        ];
        _initialiSubscriptions =
        [
            _blockchain.TipChanged.Subscribe(Blockchain_TipChanged),
            _blockchain.BlockExecuted.Subscribe(Blockchain_BlockExecuted),
            // _transport.MessageRouter.Register<ConsensusProposalClaimMessage>(HandleProposalClaimMessageAsync),
            // _transport.MessageRouter.Register<ConsensusVoteBitsMessage>(HandleVoteBitsMessageAsync),
            // _transport.MessageRouter.Register<ConsensusMaj23Message>(HandleMaj23MessageAsync),
            // _transport.MessageRouter.Register<ConsensusMessage>(HandleMessageAsync),
            _transport.MessageRouter.RegisterSendingMessageValidation<ConsensusVoteMessage>(ValidateMessageToSend),
            _transport.MessageRouter.RegisterReceivedMessageValidation<ConsensusVoteMessage>(ValidateMessageToReceive),
        ];
    }

    public IObservable<int> HeightChanged => _heightChangedSubject;

    public IObservable<Round> RoundChanged => _roundChangedSubject;

    public IObservable<ConsensusStep> StepChanged => _stepChangedSubject;

    public IObservable<ConsensusStep> TimeoutOccurred => _timeoutOccurredSubject;

    public IObservable<Proposal> BlockProposed => _blockProposedSubject;

    public Address Address => _signer.Address;

    public int Height { get; private set; }

    public int Round { get; private set; }

    public ConsensusStep Step { get; private set; }

    public Consensus Consensus => _consensus;

    public ImmutableArray<Peer> Validators => [.. _gossip.Peers];

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _dispatcher = new Dispatcher();
        await _consensus.StartAsync(default);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        if (_dispatcher is not null)
        {
            await _dispatcher.DisposeAsync();
            _dispatcher = null;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        Array.ForEach(_runningSubscriptions, subscription => subscription.Dispose());
        _runningSubscriptions = [];
        await _gossip.DisposeAsync();
        _peerExplorer.Dispose();
        Array.ForEach(_initialiSubscriptions, subscription => subscription.Dispose());
        _broadcastingResponder.Dispose();
        _broadcaster.Dispose();
        _controller.Dispose();
        await _consensus.DisposeAsync();
        await _transport.DisposeAsync();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _peers.Clear();
        await base.DisposeAsyncCore();
    }

    private async Task NewHeightAsync(int height, CancellationToken cancellationToken)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        if (height <= Height)
        {
            var message = $"Given new height #{height} must be greater than the current height #{Height}.";
            throw new ArgumentOutOfRangeException(nameof(height), message);
        }

        await _dispatcher.InvokeAsync(async cancellationToken =>
        {
            Array.ForEach(_runningSubscriptions, subscription => subscription.Dispose());

            _broadcastingResponder.Dispose();
            _broadcaster.Dispose();
            _controller.Dispose();
            await _consensus.StopAsync(cancellationToken);
            await _consensus.DisposeAsync();
            _consensus = new Consensus(height, _blockchain.GetValidators(height), _consensusOption);
            _controller = new ConsensusController(_signer, _consensus, _blockchain);
            _broadcaster = new ConsensusBroadcaster(_controller, _gossip);
            _broadcastingResponder = new ConsensusBroadcastingResponder(_signer, _consensus, _gossip);
            _runningSubscriptions =
            [
                // .. Subscribe(_controller),
                .. Subscribe(_consensus),
            ];
            await _consensus.StartAsync(cancellationToken);
            Height = height;
            _peerCatchupRounds.Clear();
            _gossip.DeniedPeers.Clear();
            _heightChangedSubject.OnNext(Height);
            FlushPendingMessages(height);
        }, cancellationToken);
    }

    private void ValidateMessageToReceive(ConsensusVoteMessage message, MessageEnvelope messageEnvelope)
    {
        var sender = messageEnvelope.Sender;
        if (message.Height != Height)
        {
            throw new InvalidOperationException(
                $"Filtered vote from different height: {message.Height}");
        }

        if (message.Height == Height && message.Round > Round)
        {
            _peerCatchupRounds.AddOrUpdate(
                sender,
                [message.Round],
                (peer, set) => set.Add(message.Round));

            if (_peerCatchupRounds.TryGetValue(sender, out var set) && set.Count > 2)
            {
                _gossip.DeniedPeers.Add(sender);
                throw new InvalidOperationException(
                    $"Add {sender} to deny set, since repetitively found higher rounds: " +
                    $"{string.Join(", ", _peerCatchupRounds[sender])}");
            }
        }
    }

    private void ValidateMessageToSend(ConsensusVoteMessage message)
    {
        if (message.Height != Height)
        {
            throw new InvalidOperationException(
                $"Cannot send vote of height different from context's");
        }

        if (message.Round > Round)
        {
            throw new InvalidOperationException(
                $"Cannot send vote of round higher than context's");
        }
    }

    // private IEnumerable<IDisposable> Subscribe(ConsensusController consensusController)
    // {
    //     yield return consensusController.PreVoted.Subscribe(
    //         e => _gossip.Broadcast(new ConsensusPreVoteMessage { PreVote = e }));
    //     yield return consensusController.PreCommitted.Subscribe(
    //         e => _gossip.Broadcast(new ConsensusPreCommitMessage { PreCommit = e }));
    //     yield return consensusController.ProposalClaimed.Subscribe(
    //         e => _gossip.Broadcast(new ConsensusProposalClaimMessage { ProposalClaim = e }));
    //     yield return consensusController.Proposed.Subscribe(
    //         e => _gossip.Broadcast(new ConsensusProposalMessage { Proposal = e }));
    //     yield return consensusController.Majority23Observed.Subscribe(
    //         e => _gossip.Broadcast(new ConsensusMaj23Message { Maj23 = e }));
    // }

    private IEnumerable<IDisposable> Subscribe(Consensus consensus)
    {
        yield return consensus.ExceptionOccurred.Subscribe(exception =>
        {
            if (exception is EvidenceException evidenceException)
            {
                _evidenceCollector.Add(evidenceException);
            }
        });
        yield return consensus.TimeoutOccurred.Subscribe(e =>
        {
            _dispatcher?.Post(() =>
            {
                _timeoutOccurredSubject.OnNext(e);
            });
        });
        yield return consensus.RoundChanged.Subscribe(round =>
        {
            _dispatcher?.Post(() =>
            {
                Round = round.Index;
                _gossip.Messages.Clear();
                _roundChangedSubject.OnNext(round);
            });
        });
        yield return consensus.StepChanged.Subscribe(e =>
        {
            _dispatcher?.Post(() =>
            {
                Step = e.Step;
                _stepChangedSubject.OnNext(e.Step);
            });
        });

        yield return consensus.Finalized.Subscribe(e =>
        {
            var block = e.Block;
            var blockCommit = e.BlockCommit;
            _ = Task.Run(() => _blockchain.Append(block, blockCommit));
        });
        yield return consensus.Proposed.Subscribe(e =>
        {
            _blockProposedSubject.OnNext(e);
        });
    }

    private void FlushPendingMessages(int height)
    {
        foreach (var message in _pendingMessages)
        {
            if (message.Height == height)
            {
                HandleMessageInternal(message);
            }
        }

        _pendingMessages.RemoveWhere(message => message.Height <= height);
    }

    // private async Task HandleProposalClaimMessageAsync(
    //     ConsensusProposalClaimMessage consensusMessage, CancellationToken cancellationToken)
    // {
    //     if (_dispatcher is null)
    //     {
    //         throw new InvalidOperationException("Consensus reactor is not running.");
    //     }

    //     await _dispatcher.InvokeAsync(_ => Action(), cancellationToken);

    //     void Action()
    //     {
    //         var proposalClaim = consensusMessage.ProposalClaim;
    //         if (_consensus.Height == proposalClaim.Height && _consensus.Proposal is not null)
    //         {
    //             var reply = new ConsensusProposalMessage { Proposal = _consensus.Proposal };
    //             var sender = _gossip.Peers.First(
    //                 peer => peer.Address.Equals(proposalClaim.Validator));

    //             _gossip.Broadcast([sender], reply);
    //         }
    //     }
    // }

    // private async Task HandleVoteBitsMessageAsync(
    //     ConsensusVoteBitsMessage consensusMessage, CancellationToken cancellationToken)
    // {
    //     if (_dispatcher is null)
    //     {
    //         throw new InvalidOperationException("Consensus reactor is not running.");
    //     }

    //     await _dispatcher.InvokeAsync(_ => Action(), cancellationToken);

    //     void Action()
    //     {
    //         var voteBits = consensusMessage.VoteBits;
    //         var consensus = _consensus;
    //         var bits = voteBits.Bits;
    //         if (consensus.Height == voteBits.Height)
    //         {
    //             var voteType = voteBits.VoteType;
    //             var votes = voteType == VoteType.PreVote
    //                 ? consensus.Round.PreVotes.GetVotes(bits)
    //                 : consensus.Round.PreCommits.GetVotes(bits);
    //             var messageList = new List<ConsensusMessage>();
    //             foreach (var vote in votes)
    //             {
    //                 if (voteType == VoteType.PreVote)
    //                 {
    //                     messageList.Add(new ConsensusPreVoteMessage { PreVote = vote });
    //                 }
    //                 else
    //                 {
    //                     messageList.Add(new ConsensusPreCommitMessage { PreCommit = vote });
    //                 }
    //             }

    //             var sender = _gossip.Peers.First(peer => peer.Address.Equals(consensusMessage.Validator));
    //             _gossip.Broadcast([sender], [.. messageList]);
    //         }
    //     }
    // }

    // private async Task HandleMaj23MessageAsync(ConsensusMaj23Message consensusMessage, CancellationToken cancellationToken)
    // {
    //     if (_dispatcher is null)
    //     {
    //         throw new InvalidOperationException("Consensus reactor is not running.");
    //     }

    //     await _dispatcher.InvokeAsync(_ => Action(), cancellationToken);

    //     void Action()
    //     {
    //         var maj23 = consensusMessage.Maj23;
    //         var consensus = _consensus;
    //         if (consensus.Height == maj23.Height && consensus.AddPreVoteMaj23(maj23))
    //         {
    //             var round = consensus.Rounds[maj23.Round];
    //             var votes = maj23.VoteType == VoteType.PreVote ? round.PreVotes : round.PreCommits;
    //             var voteBits = new VoteBitsMetadata
    //             {
    //                 Height = consensus.Height,
    //                 Round = maj23.Round,
    //                 BlockHash = maj23.BlockHash,
    //                 Timestamp = DateTimeOffset.UtcNow,
    //                 Validator = maj23.Validator,
    //                 VoteType = maj23.VoteType,
    //                 Bits = votes.GetBits(maj23.BlockHash),
    //             }.Sign(_signer);

    //             var validator = maj23.Validator;
    //             var sender = _gossip.Peers.First(peer => peer.Address.Equals(validator));
    //             _gossip.Broadcast([sender], new ConsensusVoteBitsMessage { VoteBits = voteBits });
    //         }
    //     }
    // }

    // private Task<bool> HandleMessageAsync(ConsensusMessage consensusMessage, CancellationToken cancellationToken)
    // {
    //     if (_dispatcher is null)
    //     {
    //         throw new InvalidOperationException("Consensus reactor is not running.");
    //     }

    //     return _dispatcher.InvokeAsync(_ => HandleMessage(consensusMessage), cancellationToken);
    // }

    // private bool HandleMessage(ConsensusMessage consensusMessage)
    // {
    //     if (_dispatcher is null)
    //     {
    //         throw new InvalidOperationException("Consensus reactor is not running.");
    //     }

    //     _dispatcher.VerifyAccess();

    //     var height = consensusMessage.Height;
    //     if (height < Height)
    //     {
    //         return false;
    //     }

    //     if (_consensus.Height == height)
    //     {
    //         HandleMessageInternal(consensusMessage);
    //     }
    //     else
    //     {
    //         _pendingMessages.Add(consensusMessage);
    //     }

    //     return true;
    // }

    // private void HandleMessageInternal(ConsensusMessage consensusMessage)
    // {
    //     if (consensusMessage.Height != Height)
    //     {
    //         var message = $"ConsensusMessage height {consensusMessage.Height} does not match expected height {Height}.";
    //         throw new ArgumentException(message, nameof(consensusMessage));
    //     }

    //     if (consensusMessage is ConsensusPreVoteMessage preVoteMessage)
    //     {
    //         _consensus.PostPreVote(preVoteMessage.PreVote);
    //     }
    //     else if (consensusMessage is ConsensusPreCommitMessage preCommitMessage)
    //     {
    //         _consensus.PostPreCommit(preCommitMessage.PreCommit);
    //     }
    //     else if (consensusMessage is ConsensusProposalMessage proposalMessage)
    //     {
    //         _consensus.PostPropose(proposalMessage.Proposal);
    //     }
    // }

    private void Blockchain_TipChanged(TipChangedInfo e)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _tipChangedTime = DateTimeOffset.UtcNow;
    }

    private async void Blockchain_BlockExecuted(BlockExecutionInfo e)
    {
        var height = e.Block.Header.Height;
        var dateTime = DateTimeOffset.UtcNow;
        var delay = EnsureNonNegative(_newHeightDelay - (dateTime - _tipChangedTime));
        _cancellationTokenSource = CreateCancellationTokenSource();
        await Task.Delay(delay, StoppingToken);
        AddEvidenceToBlockChain(height);
        await NewHeightAsync(height + 1, StoppingToken);

        static TimeSpan EnsureNonNegative(TimeSpan timeSpan) => timeSpan < TimeSpan.Zero ? TimeSpan.Zero : timeSpan;
    }

    private void AddEvidenceToBlockChain(int height)
    {
        var evidenceExceptions = _evidenceCollector.Flush().Where(item => item.Height <= height).ToArray();
        foreach (var evidenceException in evidenceExceptions)
        {
            try
            {
                var validators = _blockchain.GetWorld(evidenceException.Height).GetValidators();
                var evidenceContext = new EvidenceContext(validators);
                var evidence = evidenceException.Create(evidenceContext);
                _blockchain.PendingEvidence.Add(evidence);
            }
            catch
            {
                // logging
            }
        }
    }
}
