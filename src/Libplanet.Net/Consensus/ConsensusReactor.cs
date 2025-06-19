using System.Text.Json;
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
    private readonly ContextOptions _contextOption;
    private readonly MessageCommunicator _messageCommunicator;
    private readonly Blockchain _blockchain;
    private readonly PrivateKey _privateKey;
    private readonly TimeSpan _newHeightDelay;
    private readonly HashSet<ConsensusMessage> _pendingMessages = [];
    private readonly EvidenceExceptionCollector _evidenceCollector = new();
    private readonly IDisposable _tipChangedSubscription;

    private Context _currentContext;
    private CancellationTokenSource? _newHeightCts;
    private bool _disposed;

    public ConsensusReactor(ITransport transport, Blockchain blockchain, ConsensusReactorOptions options)
    {
        _messageCommunicator = new MessageCommunicator(
            transport,
            options.ConsensusPeers,
            options.SeedPeers,
            ProcessMessage);
        _gossip = _messageCommunicator.Gossip;

        _blockchain = blockchain;
        _privateKey = options.PrivateKey;
        _newHeightDelay = options.TargetBlockInterval;

        _contextOption = options.ContextOptions;
        _currentContext = new Context(
            _blockchain,
            _blockchain.Tip.Height + 1,
            _blockchain.BlockCommits[_blockchain.Tip.Height],
            _privateKey.AsSigner(),
            contextOption: _contextOption);
        AttachEventHandlers(_currentContext);

        _tipChangedSubscription = _blockchain.TipChanged.Subscribe(OnTipChanged);
    }

    public event EventHandler<(int Height, ConsensusMessage Message)>? MessagePublished;

    internal event EventHandler<(int Height, Exception)>? ExceptionOccurred;

    internal event EventHandler<(int Height, int Round, ConsensusStep Step)>? TimeoutProcessed;

    internal event EventHandler<ContextState>? StateChanged;

    internal event EventHandler<(int Height, ConsensusMessage Message)>? MessageConsumed;

    internal event EventHandler<(int Height, Action)>? MutationConsumed;

    private void AttachEventHandlers(Context context)
    {
        // NOTE: Events for testing and debugging.
        context.ExceptionOccurred.Subscribe(exception => ExceptionOccurred?.Invoke(this, (context.Height, exception)));
        context.TimeoutProcessed += (sender, eventArgs) =>
            TimeoutProcessed?.Invoke(this, (context.Height, eventArgs.Round, eventArgs.Step));
        context.StateChanged.Subscribe(state => StateChanged?.Invoke(this, state));
        context.MessageConsumed += (sender, message) =>
            MessageConsumed?.Invoke(this, (context.Height, message));
        context.MutationConsumed += (sender, action) =>
            MutationConsumed?.Invoke(this, (context.Height, action));

        // NOTE: Events for consensus logic.
        context.HeightStarted.Subscribe(_messageCommunicator.StartHeight);
        context.RoundStarted.Subscribe(_messageCommunicator.StartRound);
        context.MessagePublished.Subscribe(message =>
        {
            _gossip.PublishMessage(message);
            MessagePublished?.Invoke(this, (context.Height, message));
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
            await _gossip.DisposeAsync();
            if (_newHeightCts is not null)
            {
                await _newHeightCts.CancelAsync();
            }

            await _currentContext.DisposeAsync();

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
        await _currentContext.StartAsync(default);
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

    internal Context CurrentContext
    {
        get
        {
            lock (_contextLock)
            {
                return _currentContext;
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
        if (_currentContext.Height == height - 1 &&
            _currentContext.GetBlockCommit() is { } prevCommit)
        {
            lastCommit = prevCommit;
        }

        if (lastCommit == default &&
            _blockchain.BlockCommits[height - 1] is { } storedCommit)
        {
            lastCommit = storedCommit;
        }

        await _currentContext.DisposeAsync();
        _currentContext = new Context(
            _blockchain,
            height,
            lastCommit,
            _privateKey.AsSigner(),
            contextOption: _contextOption);
        AttachEventHandlers(_currentContext);

        foreach (var message in _pendingMessages)
        {
            if (message.Height == height)
            {
                _currentContext.ProduceMessage(message);
            }
        }

        _pendingMessages.RemoveWhere(message => message.Height <= height);
        if (IsRunning)
        {
            await _currentContext.StartAsync(default);
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
            if (_currentContext.Height == height)
            {
                _currentContext.ProduceMessage(consensusMessage);
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
                if (_currentContext.Height == height)
                {
                    return _currentContext.AddMaj23(maj23);
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
                if (_currentContext.Height == height)
                {
                    // NOTE: Should check if collected messages have same BlockHash with
                    // VoteSetBit's BlockHash?
                    return _currentContext.GetVoteSetBitsResponse(voteSetBits);
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
                if (_currentContext.Height == height)
                {
                    // NOTE: Should check if collected messages have same BlockHash with
                    // VoteSetBit's BlockHash?
                    return _currentContext.Proposal;
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
        var evidenceExceptions = _currentContext.CollectEvidenceExceptions();
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
