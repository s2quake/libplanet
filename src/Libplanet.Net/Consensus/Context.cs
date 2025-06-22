using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public partial class Context : IAsyncDisposable
{
    private readonly ContextOptions _contextOptions;

    private readonly Subject<int> _heightStartedSubject = new();
    private readonly Subject<int> _roundStartedSubject = new();
    private readonly Subject<ConsensusMessage> _messagePublishedSubject = new();
    private readonly Subject<Exception> _exceptionOccurredSubject = new();
    private readonly Subject<ContextState> _stateChangedSubject = new();

    private readonly Blockchain _blockchain;
    private readonly ImmutableSortedSet<Validator> _validators;
    private readonly Channel<ConsensusMessage> _messageRequests;
    private readonly Channel<Action> _mutationRequests;
    private readonly HeightVoteSet _heightVoteSet;
    private readonly ISigner _signer;
    private readonly HashSet<int> _hasTwoThirdsPreVoteTypes = [];
    private readonly HashSet<int> _preVoteTimeoutFlags = [];
    private readonly HashSet<int> _preCommitTimeoutFlags = [];
    private readonly HashSet<int> _preCommitWaitFlags = [];
    private readonly HashSet<int> _endCommitWaitFlags = [];
    private readonly EvidenceExceptionCollector _evidenceCollector = new();
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ICache<BlockHash, bool> _blockValidationCache;

    private Proposal? _proposal;
    private Block? _proposalBlock;
    private Block? _lockedValue;
    private int _lockedRound = -1;
    private Block? _validValue;
    private int _validRound = -1;
    private Block? _decision;
    private int _committedRound = -1;
    private readonly BlockCommit _lastCommit;
    private bool _disposed;

    public Context(
        Blockchain blockchain,
        int height,
        BlockCommit previousCommit,
        ISigner signer,
        ContextOptions? contextOption = null)
    {
        if (height < 1)
        {
            throw new ArgumentException(
                $"Given {nameof(height)} must be positive: {height}", nameof(height));
        }

        _signer = signer;
        Height = height;
        _lastCommit = previousCommit;
        _blockchain = blockchain;
        _messageRequests = Channel.CreateUnbounded<ConsensusMessage>();
        _mutationRequests = Channel.CreateUnbounded<System.Action>();
        _validators = blockchain.GetValidators(height);
        _heightVoteSet = new HeightVoteSet(height, _validators);
        _cancellationTokenSource = new CancellationTokenSource();
        _blockValidationCache = new ConcurrentLruBuilder<BlockHash, bool>()
            .WithCapacity(128)
            .Build();

        _contextOptions = contextOption ?? new ContextOptions();
    }

    public IObservable<int> HeightStarted => _heightStartedSubject;

    public IObservable<int> RoundStarted => _roundStartedSubject;

    public IObservable<ConsensusMessage> MessagePublished => _messagePublishedSubject;

    public IObservable<Exception> ExceptionOccurred => _exceptionOccurredSubject;

    internal event EventHandler<(int Round, ConsensusStep Step)>? TimeoutProcessed;

    public IObservable<ContextState> StateChanged => _stateChangedSubject;

    internal event EventHandler<ConsensusMessage>? MessageConsumed;

    internal event EventHandler<Action>? MutationConsumed;

    internal event EventHandler<(int Round, VoteType Flag, IEnumerable<Vote> Votes)>? VoteSetModified;

    public int Height { get; }

    public int Round { get; private set; } = -1;

    public ConsensusStep Step { get; private set; }

    public Proposal? Proposal
    {
        get => _proposal;
        private set
        {
            if (value is { } p)
            {
                _proposal = p;
                // _proposalBlock = ModelSerializer.DeserializeFromBytes<Block>(p.MarshaledBlock);
            }
            else
            {
                _proposal = null;
                _proposalBlock = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _cancellationTokenSource.CancelAsync();
            _messageRequests.Writer.TryComplete();
            _mutationRequests.Writer.TryComplete();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public BlockCommit GetBlockCommit()
    {
        try
        {
            var blockCommit = _heightVoteSet.PreCommits(Round).ToBlockCommit();
            return blockCommit;
        }
        catch (KeyNotFoundException)
        {
            return BlockCommit.Empty;
        }
    }

    public VoteSetBits GetVoteSetBits(int round, BlockHash blockHash, VoteType voteType)
    {
        // If executed in correct manner (called by Maj23),
        // _heightVoteSet.PreVotes(round) on below cannot throw KeyNotFoundException,
        // since RoundVoteSet has been already created on SetPeerMaj23.
        bool[] voteBits = voteType switch
        {
            VoteType.PreVote => _heightVoteSet.PreVotes(round).BitArrayByBlockHash(blockHash),
            VoteType.PreCommit
                => _heightVoteSet.PreCommits(round).BitArrayByBlockHash(blockHash),
            _ => throw new ArgumentException(
                "VoteType should be either PreVote or PreCommit.",
                nameof(voteType)),
        };

        return new VoteSetBitsMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _signer.Address,
            Flag = voteType,
            VoteBits = [.. voteBits],
        }.Sign(_signer);
    }

    public VoteSetBits? AddMaj23(Maj23 maj23)
    {
        try
        {
            if (_heightVoteSet.SetPeerMaj23(maj23))
            {
                var voteSetBits = GetVoteSetBits(maj23.Round, maj23.BlockHash, maj23.VoteType);
                return voteSetBits.VoteBits.All(b => b) ? null : voteSetBits;
            }

            return null;
        }
        catch (InvalidMaj23Exception ime)
        {
            _exceptionOccurredSubject.OnNext(ime);
            return null;
        }
    }

    public IEnumerable<ConsensusMessage> GetVoteSetBitsResponse(VoteSetBits voteSetBits)
    {
        IEnumerable<Vote> votes;
        try
        {
            votes = voteSetBits.Flag switch
            {
                VoteType.PreVote =>
                _heightVoteSet.PreVotes(voteSetBits.Round).MappedList().Where(
                    (vote, index)
                    => !voteSetBits.VoteBits[index]
                    && vote is { }
                    && vote.Flag == VoteType.PreVote).Select(vote => vote!),
                VoteType.PreCommit =>
                _heightVoteSet.PreCommits(voteSetBits.Round).MappedList().Where(
                    (vote, index)
                    => !voteSetBits.VoteBits[index]
                    && vote is { }
                    && vote.Flag == VoteType.PreCommit).Select(vote => vote!),
                _ => throw new ArgumentException(
                    "VoteType should be PreVote or PreCommit.",
                    nameof(voteSetBits.Flag)),
            };
        }
        catch (KeyNotFoundException)
        {
            votes = Array.Empty<Vote>();
        }

        return from vote in votes
               select vote.Flag switch
               {
                   VoteType.PreVote => (ConsensusMessage)new ConsensusPreVoteMessage { PreVote = vote },
                   VoteType.PreCommit => (ConsensusMessage)new ConsensusPreCommitMessage { PreCommit = vote },
                   _ => throw new ArgumentException(
                       "VoteType should be PreVote or PreCommit.",
                       nameof(vote.Flag)),
               };
    }

    public EvidenceException[] CollectEvidenceExceptions() => _evidenceCollector.Flush();

    private TimeSpan TimeoutPreVote(long round)
    {
        return TimeSpan.FromMilliseconds(
            _contextOptions.PreVoteTimeoutBase +
            (round * _contextOptions.PreVoteTimeoutDelta));
    }

    private TimeSpan TimeoutPreCommit(long round)
    {
        return TimeSpan.FromMilliseconds(
            _contextOptions.PreCommitTimeoutBase +
            (round * _contextOptions.PreCommitTimeoutDelta));
    }

    private TimeSpan TimeoutPropose(long round)
    {
        return TimeSpan.FromMilliseconds(
            _contextOptions.ProposeTimeoutBase +
            (round * _contextOptions.ProposeTimeoutDelta));
    }

    private Block GetValue()
    {
        return _blockchain.ProposeBlock(_signer);
    }

    private bool IsValid(Block block)
    {
        if (_blockValidationCache.TryGet(block.BlockHash, out var isValid))
        {
            return isValid;
        }
        else
        {
            // Need to get txs from store, lock?
            // TODO: Remove ChainId, enhancing lock management.
            // _blockChain._rwlock.EnterUpgradeableReadLock();

            if (block.Height != Height)
            {
                _blockValidationCache.AddOrUpdate(block.BlockHash, false);
                return false;
            }

            try
            {
                block.Validate(_blockchain);
                _blockchain.Options.BlockOptions.Validate(block);

                foreach (var tx in block.Transactions)
                {
                    _blockchain.Options.TransactionOptions.Validate(tx);
                }
            }
            catch (Exception e) when (
                e is InvalidOperationException)
            {
                _blockValidationCache.AddOrUpdate(block.BlockHash, false);
                return false;
            }
            finally
            {
                // _blockChain._rwlock.ExitUpgradeableReadLock();
            }

            _blockValidationCache.AddOrUpdate(block.BlockHash, true);
            return true;
        }
    }

    private Vote MakeVote(int round, BlockHash blockHash, VoteType voteType)
    {
        if (voteType == VoteType.Null || voteType == VoteType.Unknown)
        {
            throw new ArgumentException(
                $"{nameof(voteType)} must be either {VoteType.PreVote} or {VoteType.PreCommit}" +
                $"to create a valid signed vote.");
        }

        return new VoteMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _signer.Address,
            ValidatorPower = _validators.GetValidator(_signer.Address).Power,
            Flag = voteType,
        }.Sign(_signer);
    }

    private Maj23 MakeMaj23(int round, BlockHash blockHash, VoteType voteType)
    {
        if (voteType == VoteType.Null || voteType == VoteType.Unknown)
        {
            throw new ArgumentException(
                $"{nameof(voteType)} must be either {VoteType.PreVote} or {VoteType.PreCommit}" +
                $"to create a valid signed maj23.");
        }

        return new Maj23Metadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _signer.Address,
            VoteType = voteType,
        }.Sign(_signer);
    }

    private (Block, int)? GetProposal()
        => Proposal is { } p && _proposalBlock is { } b ? (b, p.ValidRound) : null;
}
