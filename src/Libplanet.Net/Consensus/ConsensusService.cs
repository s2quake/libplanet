using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Libplanet.Net.Consensus.ConsensusMessageHandlers;
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
    private readonly Subject<(Round Round, ConsensusStep Step)> _timeoutOccurredSubject = new();

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
    private readonly IDisposable _handlerRegistration;

    private Dispatcher? _dispatcher;
    private Consensus _consensus;
    // private ConsensusCommunicator _communicator;
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

        _publishSubscriptions = [.. Subscribe(_consensus, _gossip)];
        _consensusSubscriptions = [.. Subscribe(_consensus)];
        _blockchainSubscriptions =
        [
            _blockchain.TipChanged.Subscribe(OnTipChanged),
            _blockchain.BlockExecuted.Subscribe(OnBlockExecuted),
        ];
        _handlerRegistration = _transport.MessageRouter.RegisterMany(
        [
            new ConsensusVoteSetBitsMessageHandler(this, _gossip),
            new ConsensusMaj23MessageHandler(this, _gossip),
            new ConsensusProposalClaimMessageHandler(this, _gossip),
            new ConsensusMessageHandler(this),
        ]);
        // _gossip.MessageHandlers.AddRange(_messageHandlers);
    }

    public IObservable<int> HeightChanged => _heightChangedSubject;

    public IObservable<Round> RoundChanged => _roundChangedSubject;

    public IObservable<ConsensusStep> StepChanged => _stepChangedSubject;

    public IObservable<(Round Round, ConsensusStep Step)> TimeoutOccurred => _timeoutOccurredSubject;

    public IObservable<Proposal> BlockPropose => _blockProposeSubject;

    public Address Address => _signer.Address;

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

    private static IEnumerable<IDisposable> Subscribe(Consensus consensus, Gossip gossip)
    {
        // yield return consensus.ShouldPreVote.Subscribe(vote
        //     => gossip.PublishMessage(new ConsensusPreVoteMessage { PreVote = vote }));
        // yield return consensus.ShouldPreCommit.Subscribe(vote
        //     => gossip.PublishMessage(new ConsensusPreCommitMessage { PreCommit = vote }));
        // yield return consensus.ShouldQuorumReach.Subscribe(maj23
        //     => gossip.PublishMessage(new ConsensusMaj23Message { Maj23 = maj23 }));
        // yield return consensus.ShouldProposalClaim.Subscribe(proposalClaim
        //     => gossip.PublishMessage(new ConsensusProposalClaimMessage { ProposalClaim = proposalClaim }));
        // yield return consensus.ShouldPropose.Subscribe(proposal
        //     => gossip.PublishMessage(new ConsensusProposalMessage { Proposal = proposal }));
        yield break;
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
            _dispatcher?.Post((Action)(() =>
            {
                this.Step = e.Step;
                _stepChangedSubject.OnNext(e.Step);
            }));
        });

        yield return consensus.Finalized.Subscribe(e =>
        {
            var block = e.Block;
            var blockCommit = e.BlockCommit;
            _ = Task.Run(() => _blockchain.Append(block, blockCommit));
        });
        // yield return consensus.ShouldPropose.Subscribe(proposal =>
        // {
        //     _dispatcher?.Post(() =>
        //     {
        //         _blockProposeSubject.OnNext(proposal);
        //     });
        // });
    }

    public int Height { get; private set; }

    public int Round { get; private set; }

    public ConsensusStep Step { get; private set; }

    public Consensus Consensus => _consensus;

    public ImmutableArray<Peer> Validators => [.. _gossip.Peers];

    public async Task NewHeightAsync(int height, CancellationToken cancellationToken)
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

            await _consensus.StopAsync(cancellationToken);
            await _consensus.DisposeAsync();
            _consensus = new Consensus(height, _blockchain.GetValidators(height), _consensusOption);
            _publishSubscriptions = [.. Subscribe(_consensus, _gossip)];
            _consensusSubscriptions = [.. Subscribe(_consensus)];
            await _consensus.StartAsync(cancellationToken);
            Height = height;
            _peerCatchupRounds.Clear();
            _gossip.ClearDenySet();
            _heightChangedSubject.OnNext(Height);
            FlushPendingMessages(height);
        }, cancellationToken);
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


    public Task<bool> HandleMessageAsync(ConsensusMessage consensusMessage, CancellationToken cancellationToken)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        return _dispatcher.InvokeAsync(_ => HandleMessage(consensusMessage), cancellationToken);
    }

    internal bool HandleMessage(ConsensusMessage consensusMessage)
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

    public void Post(Proposal proposal)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        _dispatcher.Post(() => _consensus.Propose(proposal));
    }

    public void PreVote(Vote vote)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        _dispatcher.Post(() => _consensus.PreVote(vote));
    }

    public void PreCommit(Vote vote)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        _dispatcher.Post(() => _consensus.PreCommit(vote));
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
            if (_consensus.Height == height && _consensus.AddPreVoteMaj23(maj23))
            {
                return _consensus.GetVoteSetBits(_signer, maj23);
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
            if (_consensus.Height == height)
            {
                // NOTE: Should check if collected messages have same BlockHash with
                // VoteSetBit's BlockHash?
                var votes = _consensus.GetVotes(voteSetBits);
                foreach (var vote in votes)
                {
                    yield return vote.Type switch
                    {
                        VoteType.PreVote => new ConsensusPreVoteMessage { PreVote = vote },
                        VoteType.PreCommit => new ConsensusPreCommitMessage { PreCommit = vote },
                        _ => throw new ArgumentException("VoteType should be PreVote or PreCommit.", nameof(vote)),
                    };
                }
            }
        }
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
            if (_consensus.Height == height)
            {
                // NOTE: Should check if collected messages have same BlockHash with
                // VoteSetBit's BlockHash?
                return _consensus.Proposal;
            }
        }

        return null;
    }

    private void OnTipChanged(TipChangedInfo e)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _tipChangedTime = DateTimeOffset.UtcNow;
    }

    private async void OnBlockExecuted(BlockExecutionInfo e)
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
        _handlerRegistration.Dispose();
        await _gossip.DisposeAsync();
        _peerExplorer.Dispose();
        Array.ForEach(_blockchainSubscriptions, subscription => subscription.Dispose());
        Array.ForEach(_consensusSubscriptions, subscription => subscription.Dispose());
        _consensusSubscriptions = [];
        await _consensus.DisposeAsync();
        await _transport.DisposeAsync();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
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
