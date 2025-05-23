using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.Blockchain;
using Libplanet.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Serilog;

namespace Libplanet.Net.Consensus;

public partial class ConsensusContext : IDisposable
{
    private readonly object _contextLock;
    private readonly ContextOption _contextOption;
    private readonly IConsensusMessageCommunicator _consensusMessageCommunicator;
    private readonly BlockChain _blockChain;
    private readonly PrivateKey _privateKey;
    private readonly TimeSpan _newHeightDelay;
    private readonly ILogger _logger;
    private readonly HashSet<ConsensusMsg> _pendingMessages;
    private readonly EvidenceExceptionCollector _evidenceCollector = new();
    private readonly IDisposable _tipChangedSubscription;

    private Context _currentContext;
    private CancellationTokenSource? _newHeightCts;

    public ConsensusContext(
        IConsensusMessageCommunicator consensusMessageCommunicator,
        BlockChain blockChain,
        PrivateKey privateKey,
        TimeSpan newHeightDelay,
        ContextOption contextOption)
    {
        _consensusMessageCommunicator = consensusMessageCommunicator;
        _blockChain = blockChain;
        _privateKey = privateKey;
        Running = false;
        _newHeightDelay = newHeightDelay;

        _contextOption = contextOption;
        _currentContext = CreateContext(
            _blockChain.Tip.Height + 1,
            _blockChain.BlockCommits[_blockChain.Tip.Height]);
        AttachEventHandlers(_currentContext);
        _pendingMessages = new HashSet<ConsensusMsg>();

        _logger = Log
            .ForContext("Tag", "Consensus")
            .ForContext("SubTag", "ConsensusContext")
            .ForContext<ConsensusContext>()
            .ForContext("Source", nameof(ConsensusContext));

        _tipChangedSubscription = _blockChain.TipChanged.Subscribe(OnTipChanged);
        _contextLock = new object();
    }

    public bool Running { get; private set; }

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
        if (Running)
        {
            throw new InvalidOperationException(
                $"Can only start {nameof(ConsensusContext)} if {nameof(Running)} is {false}.");
        }
        else
        {
            lock (_contextLock)
            {
                Running = true;
                _currentContext.Start();
            }
        }
    }

    public void Dispose()
    {
        _newHeightCts?.Cancel();
        lock (_contextLock)
        {
            _currentContext.Dispose();
        }

        _tipChangedSubscription.Dispose();
    }

    public void NewHeight(int height)
    {
        lock (_contextLock)
        {
            _logger.Information(
                "Invoked {FName}() for new height #{NewHeight} from old height #{OldHeight}",
                nameof(NewHeight),
                height,
                Height);

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
                _logger.Debug(
                    "Retrieved block commit for Height #{Height} from previous context",
                    lastCommit.Height);
            }

            if (lastCommit == default &&
                _blockChain.BlockCommits[height - 1] is { } storedCommit)
            {
                lastCommit = storedCommit;
                _logger.Debug(
                    "Retrieved stored block commit for Height #{Height} from blockchain",
                    lastCommit.Height);
            }

            _logger.Debug(
                "LastCommit for height #{Height} is {LastCommit}",
                height,
                lastCommit);

            _currentContext.Dispose();
            _logger.Information(
                "Start consensus for height #{Height} with last commit {LastCommit}",
                height,
                lastCommit);
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
            if (Running)
            {
                _currentContext.Start();
            }
        }
    }

    public bool HandleMessage(ConsensusMsg consensusMessage)
    {
        int height = consensusMessage.Height;
        if (height < Height)
        {
            _logger.Debug(
                "Discarding a received message as its height #{MessageHeight} " +
                "is lower than the current context's height #{ContextHeight}",
                height,
                Height);
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
            _logger.Debug(
                "Ignore a received VoteSetBits as its height " +
                "#{Height} is lower than the current context's height #{ContextHeight}",
                height,
                Height);
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

    public IEnumerable<ConsensusMsg> HandleVoteSetBits(VoteSetBits voteSetBits)
    {
        int height = voteSetBits.Height;
        if (height < Height)
        {
            _logger.Debug(
                "Ignore a received VoteSetBits as its height " +
                "#{Height} is lower than the current context's height #{ContextHeight}",
                height,
                Height);
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

        return Array.Empty<ConsensusMsg>();
    }

    public Proposal? HandleProposalClaim(ProposalClaim proposalClaim)
    {
        int height = proposalClaim.Height;
        int round = proposalClaim.Round;
        if (height != Height)
        {
            _logger.Debug(
                "Ignore a received ProposalClaim as its height " +
                "#{Height} does not match with the current context's height #{ContextHeight}",
                height,
                Height);
        }
        else if (round != Round)
        {
            _logger.Debug(
                "Ignore a received ProposalClaim as its round " +
                "#{Round} does not match with the current context's round #{ContextRound}",
                round,
                Round);
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
        // TODO: Should set delay by using GST.
        _newHeightCts?.Cancel();
        _newHeightCts?.Dispose();
        _newHeightCts = new CancellationTokenSource();
        Task.Run(
            async () =>
            {
                await Task.Delay(_newHeightDelay, _newHeightCts.Token);

                // Delay further until evaluation is ready.
                while (_blockChain.GetStateRootHash(e.NewTip.Height) == default)
                {
                    // FIXME: Maybe interval should be adjustable?
                    await Task.Delay(100, _newHeightCts.Token);
                }

                if (!_newHeightCts.IsCancellationRequested)
                {
                    try
                    {
                        HandleEvidenceExceptions();
                        AddEvidenceToBlockChain(e.NewTip);
                        NewHeight(e.NewTip.Height + 1);
                    }
                    catch (Exception exc)
                    {
                        _logger.Error(
                            exc,
                            "Unexpected exception occurred during {FName}()",
                            nameof(NewHeight));
                    }
                }
                else
                {
                    _logger.Error(
                        "Did not invoke {FName}() for height " +
                        "#{Height} because cancellation is requested",
                        nameof(NewHeight),
                        e.NewTip.Height + 1);
                }
            },
            _newHeightCts.Token);
    }

    private Context CreateContext(int height, BlockCommit lastCommit)
    {
        var nextStateRootHash = _blockChain.GetStateRootHash(height - 1);
        ImmutableSortedSet<Validator> validatorSet = _blockChain
            .GetWorld(nextStateRootHash)
            .GetValidators();

        Context context = new Context(
            _blockChain,
            height,
            lastCommit,
            _privateKey,
            validatorSet,
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
                var validatorSet = _blockChain.GetWorld(evidenceException.Height).GetValidators();
                var evidenceContext = new EvidenceContext(validatorSet);
                var evidence = evidenceException.Create(evidenceContext);
                _blockChain.PendingEvidences.Add(evidence);
            }
            catch (Exception e)
            {
                _logger.Error(
                    exception: e,
                    messageTemplate: "Unexpected exception occurred during {FName}()",
                    propertyValue: nameof(BlockChain.PendingEvidences));
            }
        }
    }
}
