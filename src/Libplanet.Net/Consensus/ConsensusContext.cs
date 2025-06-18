using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Serilog;
using Nito.AsyncEx;

namespace Libplanet.Net.Consensus;

public partial class ConsensusContext : IAsyncDisposable
{
    private readonly object _contextLock;
    private readonly ContextOptions _contextOption;
    private readonly MessageCommunicator _consensusMessageCommunicator;
    private readonly Blockchain _blockchain;
    private readonly PrivateKey _privateKey;
    private readonly TimeSpan _newHeightDelay;
    private readonly HashSet<ConsensusMessage> _pendingMessages;
    private readonly EvidenceExceptionCollector _evidenceCollector = new();
    private readonly IDisposable _tipChangedSubscription;

    private Context _currentContext;
    private CancellationTokenSource? _newHeightCts;
    private bool _disposed;

    public ConsensusContext(
        MessageCommunicator messageCommunicator,
        Blockchain blockchain,
        PrivateKey privateKey,
        TimeSpan newHeightDelay,
        ContextOptions contextOption)
    {
        _consensusMessageCommunicator = messageCommunicator;
        _blockchain = blockchain;
        _privateKey = privateKey;
        IsRunning = false;
        _newHeightDelay = newHeightDelay;

        _contextOption = contextOption;
        _currentContext = CreateContext(
            _blockchain.Tip.Height + 1,
            _blockchain.BlockCommits[_blockchain.Tip.Height]);
        AttachEventHandlers(_currentContext);
        _pendingMessages = new HashSet<ConsensusMessage>();

        _tipChangedSubscription = _blockchain.TipChanged.Subscribe(OnTipChanged);
        _contextLock = new object();
    }

    public bool IsRunning { get; private set; }

    public int Height => CurrentContext.Height;

    public int Round => CurrentContext.Round;

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

    public void Start()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException(
                $"Can only start {nameof(ConsensusContext)} if {nameof(IsRunning)} is {false}.");
        }
        else
        {
            lock (_contextLock)
            {
                IsRunning = true;
                _currentContext.Start();
            }
        }
    }


    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
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
        _currentContext = CreateContext(height, lastCommit);
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
            _currentContext.Start();
        }
    }

    public bool HandleMessage(ConsensusMessage consensusMessage)
    {
        int height = consensusMessage.Height;
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

    public override string ToString()
    {
        lock (_contextLock)
        {
            return _currentContext.ToString();
        }
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
            catch (Exception exc)
            {
                // logging
            }
        }
    }

    private Context CreateContext(int height, BlockCommit lastCommit)
    {
        var stateRootHash = _blockchain.GetStateRootHash(height - 1);
        var validators = _blockchain.GetWorld(stateRootHash).GetValidators();
        var context = new Context(
            _blockchain,
            height,
            lastCommit,
            _privateKey,
            validators,
            contextOption: _contextOption);
        return context;
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
}
