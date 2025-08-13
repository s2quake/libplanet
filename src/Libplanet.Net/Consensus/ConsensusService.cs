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

    private readonly Subject<Proposal> _blockProposeSubject = new();
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
    private readonly IDisposable[] _blockchainSubscriptions;
    private readonly IDisposable[] _handlerRegistrations;

    private Dispatcher? _dispatcher;
    private Consensus _consensus;
    private ConsensusController _consensusController;
    private IDisposable[] _publishSubscriptions;
    private IDisposable[] _consensusSubscriptions;
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
        _consensusController = new ConsensusController(_signer, _consensus, _blockchain);

        _publishSubscriptions = [.. Subscribe(_consensusController, _gossip)];
        _consensusSubscriptions = [.. Subscribe(_consensus)];
        _blockchainSubscriptions =
        [
            _blockchain.TipChanged.Subscribe(Blockchain_TipChanged),
            _blockchain.BlockExecuted.Subscribe(Blockchain_BlockExecuted),
        ];
        _handlerRegistrations =
        [
            _transport.MessageRouter.Register<ConsensusProposalClaimMessage>(
                m => HandleProposalClaimMessageAsync(m, StoppingToken)),
            _transport.MessageRouter.Register<ConsensusVoteBitsMessage>(
                m => HandleVoteBitsMessageAsync(m, StoppingToken)),
            _transport.MessageRouter.Register<ConsensusMaj23Message>(
                m => HandleMaj23MessageAsync(m, StoppingToken)),
            _transport.MessageRouter.Register<ConsensusMessage>(
                m => HandleMessageAsync(m, StoppingToken)),
        ];
    }

    public IObservable<int> HeightChanged => _heightChangedSubject;

    public IObservable<Round> RoundChanged => _roundChangedSubject;

    public IObservable<ConsensusStep> StepChanged => _stepChangedSubject;

    public IObservable<ConsensusStep> TimeoutOccurred => _timeoutOccurredSubject;

    public IObservable<Proposal> BlockPropose => _blockProposeSubject;

    public Address Address => _signer.Address;

    public int Height { get; private set; }

    public int Round { get; private set; }

    public ConsensusStep Step { get; private set; }

    public Consensus Consensus => _consensus;

    public ImmutableArray<Peer> Validators => [.. _gossip.Peers];

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
            Array.ForEach(_consensusSubscriptions, subscription => subscription.Dispose());
            Array.ForEach(_publishSubscriptions, subscription => subscription.Dispose());

            _consensusController.Dispose();
            await _consensus.StopAsync(cancellationToken);
            await _consensus.DisposeAsync();
            _consensus = new Consensus(height, _blockchain.GetValidators(height), _consensusOption);
            _consensusController = new ConsensusController(_signer, _consensus, _blockchain);
            _publishSubscriptions = [.. Subscribe(_consensusController, _gossip)];
            _consensusSubscriptions = [.. Subscribe(_consensus)];
            await _consensus.StartAsync(cancellationToken);
            Height = height;
            _peerCatchupRounds.Clear();
            _gossip.ClearDenySet();
            _heightChangedSubject.OnNext(Height);
            FlushPendingMessages(height);
        }, cancellationToken);
    }

    private void ValidateMessageToReceive((Peer Peer, IMessage Message) e)
    {
        if (e.Message is ConsensusVoteMessage voteMsg)
        {
            FilterDifferentHeightVote(voteMsg);
            FilterHigherRoundVoteSpam(voteMsg, e.Peer);
        }
    }

    private void ValidateMessageToSend(IMessage message)
    {
        if (message is ConsensusVoteMessage voteMsg)
        {
            if (voteMsg.Height != Height)
            {
                throw new InvalidOperationException(
                    $"Cannot send vote of height different from context's");
            }

            if (voteMsg.Round > Round)
            {
                throw new InvalidOperationException(
                    $"Cannot send vote of round higher than context's");
            }
        }
    }

    private void FilterDifferentHeightVote(ConsensusVoteMessage voteMsg)
    {
        if (voteMsg.Height != Height)
        {
            throw new InvalidOperationException(
                $"Filtered vote from different height: {voteMsg.Height}");
        }
    }

    private void FilterHigherRoundVoteSpam(ConsensusVoteMessage voteMsg, Peer peer)
    {
        if (voteMsg.Height == Height &&
            voteMsg.Round > Round)
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

    private static IEnumerable<IDisposable> Subscribe(ConsensusController consensusController, Gossip gossip)
    {
        yield return consensusController.PreVoted.Subscribe(vote
            => gossip.PublishMessage(new ConsensusPreVoteMessage { PreVote = vote }));
        yield return consensusController.PreCommitted.Subscribe(vote
            => gossip.PublishMessage(new ConsensusPreCommitMessage { PreCommit = vote }));
        yield return consensusController.ProposalClaimed.Subscribe(proposalClaim
            => gossip.PublishMessage(new ConsensusProposalClaimMessage { ProposalClaim = proposalClaim }));
        yield return consensusController.Proposed.Subscribe(proposal
            => gossip.PublishMessage(new ConsensusProposalMessage { Proposal = proposal }));
        yield return consensusController.Majority23Observed.Subscribe(maj23
            => gossip.PublishMessage(new ConsensusMaj23Message { Maj23 = maj23 }));
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
                _gossip.ClearCache();
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

    private async Task HandleProposalClaimMessageAsync(
        ConsensusProposalClaimMessage consensusMessage, CancellationToken cancellationToken)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        await _dispatcher.InvokeAsync(_ => Action(), cancellationToken);

        void Action()
        {
            var proposalClaim = consensusMessage.ProposalClaim;
            if (_consensus.Height == proposalClaim.Height && _consensus.Proposal is not null)
            {
                var reply = new ConsensusProposalMessage { Proposal = _consensus.Proposal };
                var sender = _gossip.Peers.First(
                    peer => peer.Address.Equals(proposalClaim.Validator));

                _gossip.PublishMessage([sender], reply);
            }
        }
    }

    private async Task HandleVoteBitsMessageAsync(
        ConsensusVoteBitsMessage consensusMessage, CancellationToken cancellationToken)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        await _dispatcher.InvokeAsync(_ => Action(), cancellationToken);

        void Action()
        {
            var voteBits = consensusMessage.VoteBits;
            var consensus = _consensus;
            var bits = voteBits.Bits;
            if (consensus.Height == voteBits.Height)
            {
                var voteType = voteBits.VoteType;
                var votes = voteType == VoteType.PreVote
                    ? consensus.Round.PreVotes.GetVotes(bits)
                    : consensus.Round.PreCommits.GetVotes(bits);
                var messageList = new List<ConsensusMessage>();
                foreach (var vote in votes)
                {
                    if (voteType == VoteType.PreVote)
                    {
                        messageList.Add(new ConsensusPreVoteMessage { PreVote = vote });
                    }
                    else
                    {
                        messageList.Add(new ConsensusPreCommitMessage { PreCommit = vote });
                    }
                }

                var sender = _gossip.Peers.First(peer => peer.Address.Equals(consensusMessage.Validator));
                _gossip.PublishMessage([sender], [.. messageList]);
            }
        }
    }

    private async Task HandleMaj23MessageAsync(ConsensusMaj23Message consensusMessage, CancellationToken cancellationToken)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        await _dispatcher.InvokeAsync(_ => Action(), cancellationToken);

        void Action()
        {
            var maj23 = consensusMessage.Maj23;
            var consensus = _consensus;
            if (consensus.Height == maj23.Height && consensus.AddPreVoteMaj23(maj23))
            {
                var round = consensus.Rounds[maj23.Round];
                var votes = maj23.VoteType == VoteType.PreVote ? round.PreVotes : round.PreCommits;
                var voteBits = new VoteBitsMetadata
                {
                    Height = consensus.Height,
                    Round = maj23.Round,
                    BlockHash = maj23.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = maj23.Validator,
                    VoteType = maj23.VoteType,
                    Bits = votes.GetBits(maj23.BlockHash),
                }.Sign(_signer);

                var validator = maj23.Validator;
                var sender = _gossip.Peers.First(peer => peer.Address.Equals(validator));
                _gossip.PublishMessage([sender], new ConsensusVoteBitsMessage { VoteBits = voteBits });
            }
        }
    }

    private Task<bool> HandleMessageAsync(ConsensusMessage consensusMessage, CancellationToken cancellationToken)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        return _dispatcher.InvokeAsync(_ => HandleMessage(consensusMessage), cancellationToken);
    }

    private bool HandleMessage(ConsensusMessage consensusMessage)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        _dispatcher.VerifyAccess();

        var height = consensusMessage.Height;
        if (height < Height)
        {
            return false;
        }

        if (_consensus.Height == height)
        {
            HandleMessageInternal(consensusMessage);
        }
        else
        {
            _pendingMessages.Add(consensusMessage);
        }

        return true;
    }

    private void HandleMessageInternal(ConsensusMessage consensusMessage)
    {
        if (consensusMessage.Height != Height)
        {
            var message = $"ConsensusMessage height {consensusMessage.Height} does not match expected height {Height}.";
            throw new ArgumentException(message, nameof(consensusMessage));
        }

        if (consensusMessage is ConsensusPreVoteMessage preVoteMessage)
        {
            _consensus.PreVote(preVoteMessage.PreVote);
        }
        else if (consensusMessage is ConsensusPreCommitMessage preCommitMessage)
        {
            _consensus.PreCommit(preCommitMessage.PreCommit);
        }
        else if (consensusMessage is ConsensusProposalMessage proposalMessage)
        {
            _consensus.Propose(proposalMessage.Proposal);
        }
    }

    // public Proposal? HandleProposalClaim(ProposalClaim proposalClaim)
    // {
    //     int height = proposalClaim.Height;
    //     int round = proposalClaim.Round;
    //     if (height != Height)
    //     {
    //         // logging
    //     }
    //     else if (round != Round)
    //     {
    //         // logging
    //     }
    //     else
    //     {
    //         if (_consensus.Height == height)
    //         {
    //             // NOTE: Should check if collected messages have same BlockHash with
    //             // VoteSetBit's BlockHash?
    //             return _consensus.Proposal;
    //         }
    //     }

    //     return null;
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
        using var cancellationTokenSource = CreateCancellationTokenSource();
        var height = e.Block.Header.Height;
        var dateTime = DateTimeOffset.UtcNow;
        var delay = EnsureNonNegative(_newHeightDelay - (dateTime - _tipChangedTime));
        _cancellationTokenSource = CreateCancellationTokenSource();
        await Task.Delay(delay, cancellationTokenSource.Token);
        AddEvidenceToBlockChain(height);
        await NewHeightAsync(height + 1, cancellationTokenSource.Token);

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
        Array.ForEach(_handlerRegistrations, subscription => subscription.Dispose());
        await _gossip.DisposeAsync();
        _peerExplorer.Dispose();
        Array.ForEach(_blockchainSubscriptions, subscription => subscription.Dispose());
        Array.ForEach(_consensusSubscriptions, subscription => subscription.Dispose());
        _consensusSubscriptions = [];
        _consensusController.Dispose();
        await _consensus.DisposeAsync();
        await _transport.DisposeAsync();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _peers.Clear();
        await base.DisposeAsyncCore();
    }

    // private void ProcessMessage(IMessage message)
    // {
    //     if (_dispatcher is null)
    //     {
    //         throw new InvalidOperationException("Consensus reactor is not running.");
    //     }

    //     _dispatcher.Post(() =>
    //     {
    //         // if (_messageHandlers.TryGetHandler(message, out var handler))
    //         // {
    //         //     handler.HandleAsync(message, default).GetAwaiter().GetResult();
    //         //     return;
    //         // }
    //         switch (message)
    //         {
    //             case ConsensusVoteSetBitsMessage voteSetBits:
    //                 // Note: ConsensusVoteSetBitsMsg will not be stored to context's message log.
    //                 var messages = HandleVoteSetBits(voteSetBits.VoteSetBits);
    //                 try
    //                 {
    //                     var sender = _gossip.Peers.First(peer => peer.Address.Equals(voteSetBits.Validator));
    //                     _gossip.PublishMessage([sender], [.. messages]);
    //                 }
    //                 catch (InvalidOperationException)
    //                 {
    //                     // logging
    //                 }

    //                 break;

    //             case ConsensusMaj23Message maj23Message:
    //                 try
    //                 {
    //                     VoteSetBits? voteSetBits = HandleMaj23(maj23Message.Maj23);
    //                     if (voteSetBits is null)
    //                     {
    //                         break;
    //                     }

    //                     var sender = _gossip.Peers.First(peer => peer.Address.Equals(maj23Message.Validator));
    //                     _gossip.PublishMessage(
    //                         [sender],
    //                         new ConsensusVoteSetBitsMessage { VoteSetBits = voteSetBits });
    //                 }
    //                 catch (InvalidOperationException)
    //                 {
    //                     // logging
    //                 }

    //                 break;

    //             case ConsensusProposalClaimMessage proposalClaimmessage:
    //                 try
    //                 {
    //                     Proposal? proposal = HandleProposalClaim(proposalClaimmessage.ProposalClaim);
    //                     if (proposal is { } proposalNotNull)
    //                     {
    //                         var reply = new ConsensusProposalMessage { Proposal = proposalNotNull };
    //                         var sender = _gossip.Peers.First(
    //                             peer => peer.Address.Equals(proposalClaimmessage.Validator));

    //                         _gossip.PublishMessage([sender], reply);
    //                     }
    //                 }
    //                 catch (InvalidOperationException)
    //                 {
    //                     // logging
    //                 }

    //                 break;

    //             case ConsensusMessage consensusMessage:
    //                 HandleMessage(consensusMessage);
    //                 break;
    //         }
    //     });
    // }
}
