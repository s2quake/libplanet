using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;
using Libplanet.Net.Transports;
using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class ConsensusReactor : IAsyncDisposable
{
    private readonly Subject<int> _heightChangedSubject = new();
    private readonly Subject<int> _roundChangedSubject = new();
    private readonly Subject<ConsensusStep> _stepChangedSubject = new();
    private readonly Subject<(int Round, ConsensusStep Step)> _timeoutOccurredSubject = new();

    private readonly Subject<Proposal> _blockProposeSubject = new();
    private readonly ITransport _transport;
    private readonly Gossip _gossip;
    private readonly ConsensusOptions _consensusOption;
    private readonly Blockchain _blockchain;
    private readonly ISigner _signer;
    private readonly TimeSpan _newHeightDelay;
    private readonly HashSet<ConsensusMessage> _pendingMessages = [];
    private readonly EvidenceCollector _evidenceCollector = new();
    private readonly ConcurrentDictionary<Peer, ImmutableHashSet<int>> _peerCatchupRounds = new();
    private readonly IDisposable[] _blockchainSubscriptions;

    private Dispatcher? _dispatcher;
    private Consensus _consensus;
    private ConsensusCommunicator _communicator;
    private IDisposable[] _gossipSubscriptions = [];
    private IDisposable[] _consensusSubscriptions;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTimeOffset _tipChangedTime;
    private bool _disposed;

    public ConsensusReactor(
        ISigner signer, Blockchain blockchain, ConsensusReactorOptions options)
    {
        _signer = signer;
        _transport = new NetMQTransport(signer, options.TransportOptions);
        _gossip = new Gossip(
            _transport,
            options.Seeds,
            [.. options.Validators.Where(item => item.Address != signer.Address)],
            options.GossipOptions);

        _blockchain = blockchain;
        _newHeightDelay = options.TargetBlockInterval;
        _consensusOption = options.ConsensusOptions;
        Height = _blockchain.Tip.Height + 1;
        _consensus = new Consensus(_blockchain, Height, _signer, _consensusOption);
        _communicator = new ConsensusCommunicator(_consensus, _gossip);
        _consensusSubscriptions = [.. Subscribe(_consensus)];
        _blockchainSubscriptions =
        [
            _blockchain.TipChanged.Subscribe(OnTipChanged),
            _blockchain.BlockExecuted.Subscribe(OnBlockExecuted),
        ];
    }

    public IObservable<int> HeightChanged => _heightChangedSubject;

    public IObservable<int> RoundChanged => _roundChangedSubject;

    public IObservable<ConsensusStep> StepChanged => _stepChangedSubject;

    public IObservable<(int Round, ConsensusStep Step)> TimeoutOccurred => _timeoutOccurredSubject;

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
                Round = round;
                _gossip.ClearCache();
                _roundChangedSubject.OnNext(round);
            });
        });
        yield return consensus.StepChanged.Subscribe((Action<ConsensusStep>)(step =>
        {
            _dispatcher?.Post((Action)(() =>
            {
                this.Step = step;
                _stepChangedSubject.OnNext(step);
            }));
        }));

        yield return consensus.Completed.Subscribe(e =>
        {
            var block = e.Block;
            var blockCommit = e.BlockCommit;
            _ = Task.Run(() => _blockchain.Append(block, blockCommit));
        });
        yield return consensus.BlockPropose.Subscribe(proposal =>
        {
            _dispatcher?.Post(() =>
            {
                _blockProposeSubject.OnNext(proposal);
            });
        });
    }

    public bool IsRunning { get; private set; }

    public int Height { get; private set; }

    public int Round { get; private set; }

    public ConsensusStep Step { get; private set; }

    public Consensus Consensus => _consensus;

    public ImmutableArray<Peer> Validators => _gossip.Peers;

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            Array.ForEach(_gossipSubscriptions, subscription => subscription.Dispose());
            await _gossip.DisposeAsync();
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            Array.ForEach(_blockchainSubscriptions, subscription => subscription.Dispose());
            Array.ForEach(_consensusSubscriptions, subscription => subscription.Dispose());
            _consensusSubscriptions = [];
            await _consensus.DisposeAsync();
            await _transport.DisposeAsync();

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
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

        _dispatcher = new Dispatcher();
        _gossipSubscriptions =
        [
            _gossip.ValidateReceivedMessage.Subscribe(ValidateMessageToReceive),
            _gossip.ValidateSendingMessage.Subscribe(ValidateMessageToSend),
            _gossip.ProcessMessage.Subscribe(ProcessMessage),
        ];
        await _gossip.StartAsync(cancellationToken);
        await _consensus.StartAsync(default);
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning || _dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        await _gossip.StopAsync(cancellationToken);
        Array.ForEach(_gossipSubscriptions, subscription => subscription.Dispose());
        await _dispatcher.DisposeAsync();
        _dispatcher = null;
        IsRunning = false;
    }

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
            _communicator.Dispose();
            await _consensus.StopAsync(cancellationToken);
            await _consensus.DisposeAsync();
            _consensus = new Consensus(_blockchain, height, _signer, _consensusOption);
            _communicator = new ConsensusCommunicator(_consensus, _gossip);
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
                _consensus.Post(message);
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
            _consensus.Post(consensusMessage);
        }
        else
        {
            _pendingMessages.Add(consensusMessage);
        }

        return true;
    }

    public void Post(Proposal proposal)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        _dispatcher.Post(() => _consensus.Post(proposal));
    }

    public void Post(Vote vote)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        _dispatcher.Post(() => _consensus.Post(vote));
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
            if (_consensus.Height == height)
            {
                return _consensus.AddMaj23(maj23);
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
                return _consensus.GetVoteSetBitsResponse(voteSetBits);
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
        var height = e.Block.Header.Height;
        var dateTime = DateTimeOffset.UtcNow;
        var delay = EnsureNonNegative(_newHeightDelay - (dateTime - _tipChangedTime));
        _cancellationTokenSource = new CancellationTokenSource();
        await Task.Delay(delay, _cancellationTokenSource.Token);
        AddEvidenceToBlockChain(height);
        await NewHeightAsync(height + 1, _cancellationTokenSource.Token);

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
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus reactor is not running.");
        }

        _dispatcher.Post(() =>
{
    switch (message)
    {
        case ConsensusVoteSetBitsMessage voteSetBits:
            // Note: ConsensusVoteSetBitsMsg will not be stored to context's message log.
            var messages = HandleVoteSetBits(voteSetBits.VoteSetBits);
            try
            {
                var sender = _gossip.Peers.First(peer => peer.Address.Equals(voteSetBits.Validator));
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

                var sender = _gossip.Peers.First(peer => peer.Address.Equals(maj23Message.Validator));
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
                Proposal? proposal = HandleProposalClaim(proposalClaimmessage.ProposalClaim);
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
});
    }
}
