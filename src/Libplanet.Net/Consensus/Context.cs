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

    private readonly Blockchain _blockchain;
    private readonly ImmutableSortedSet<Validator> _validators;
    private readonly Channel<ConsensusMessage> _messageRequests;
    private readonly Channel<System.Action> _mutationRequests;
    private readonly HeightVoteSet _heightVoteSet;
    private readonly PrivateKey _privateKey;
    private readonly HashSet<int> _hasTwoThirdsPreVoteFlags = [];
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
        Blockchain blockChain,
        int height,
        BlockCommit lastCommit,
        PrivateKey privateKey,
        ImmutableSortedSet<Validator> validators,
        ConsensusStep consensusStep = ConsensusStep.Default,
        int round = -1,
        int cacheSize = 128,
        ContextOptions? contextOption = null)
    {
        if (height < 1)
        {
            throw new ArgumentException(
                $"Given {nameof(height)} must be positive: {height}", nameof(height));
        }

        _privateKey = privateKey;
        Height = height;
        Round = round;
        Step = consensusStep;
        _lastCommit = lastCommit;
        _blockchain = blockChain;
        _messageRequests = Channel.CreateUnbounded<ConsensusMessage>();
        _mutationRequests = Channel.CreateUnbounded<System.Action>();
        _heightVoteSet = new HeightVoteSet(height, validators);
        _validators = validators;
        _cancellationTokenSource = new CancellationTokenSource();
        _blockValidationCache = new ConcurrentLruBuilder<BlockHash, bool>()
            .WithCapacity(cacheSize)
            .Build();

        _contextOptions = contextOption ?? new ContextOptions();
    }

    public int Height { get; }

    public int Round { get; private set; }

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

    public VoteSetBits GetVoteSetBits(int round, BlockHash blockHash, VoteFlag voteFlag)
    {
        // If executed in correct manner (called by Maj23),
        // _heightVoteSet.PreVotes(round) on below cannot throw KeyNotFoundException,
        // since RoundVoteSet has been already created on SetPeerMaj23.
        bool[] voteBits = voteFlag switch
        {
            VoteFlag.PreVote => _heightVoteSet.PreVotes(round).BitArrayByBlockHash(blockHash),
            VoteFlag.PreCommit
                => _heightVoteSet.PreCommits(round).BitArrayByBlockHash(blockHash),
            _ => throw new ArgumentException(
                "VoteFlag should be either PreVote or PreCommit.",
                nameof(voteFlag)),
        };

        return new VoteSetBitsMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _privateKey.Address,
            Flag = voteFlag,
            VoteBits = [.. voteBits],
        }.Sign(_privateKey);
    }

    public VoteSetBits? AddMaj23(Maj23 maj23)
    {
        try
        {
            if (_heightVoteSet.SetPeerMaj23(maj23))
            {
                var voteSetBits = GetVoteSetBits(maj23.Round, maj23.BlockHash, maj23.VoteFlag);
                return voteSetBits.VoteBits.All(b => b) ? null : voteSetBits;
            }

            return null;
        }
        catch (InvalidMaj23Exception ime)
        {
            ExceptionOccurred?.Invoke(this, ime);
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
                VoteFlag.PreVote =>
                _heightVoteSet.PreVotes(voteSetBits.Round).MappedList().Where(
                    (vote, index)
                    => !voteSetBits.VoteBits[index]
                    && vote is { }
                    && vote.Flag == VoteFlag.PreVote).Select(vote => vote!),
                VoteFlag.PreCommit =>
                _heightVoteSet.PreCommits(voteSetBits.Round).MappedList().Where(
                    (vote, index)
                    => !voteSetBits.VoteBits[index]
                    && vote is { }
                    && vote.Flag == VoteFlag.PreCommit).Select(vote => vote!),
                _ => throw new ArgumentException(
                    "VoteFlag should be PreVote or PreCommit.",
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
                   VoteFlag.PreVote => (ConsensusMessage)new ConsensusPreVoteMessage { PreVote = vote },
                   VoteFlag.PreCommit => (ConsensusMessage)new ConsensusPreCommitMessage { PreCommit = vote },
                   _ => throw new ArgumentException(
                       "VoteFlag should be PreVote or PreCommit.",
                       nameof(vote.Flag)),
               };
    }

    /// <summary>
    /// Returns the summary of context in JSON-formatted string.
    /// </summary>
    /// <returns>Returns a JSON-formatted string of context state.</returns>
    public override string ToString()
    {
        var dict = new Dictionary<string, object>
        {
            { "node_id", _privateKey.Address.ToString() },
            { "number_of_validators", _validators.Count },
            { "height", Height },
            { "round", Round },
            { "step", Step.ToString() },
            { "proposal", Proposal?.ToString() ?? "null" },
            { "locked_value", _lockedValue?.BlockHash.ToString() ?? "null" },
            { "locked_round", _lockedRound },
            { "valid_value", _validValue?.BlockHash.ToString() ?? "null" },
            { "valid_round", _validRound },
        };
        return JsonSerializer.Serialize(dict);
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
        return _blockchain.ProposeBlock(_privateKey);
    }

    private void PublishMessage(ConsensusMessage message)
        => MessageToPublish?.Invoke(this, message);

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

    private Vote MakeVote(int round, BlockHash blockHash, VoteFlag voteFlag)
    {
        if (voteFlag == VoteFlag.Null || voteFlag == VoteFlag.Unknown)
        {
            throw new ArgumentException(
                $"{nameof(voteFlag)} must be either {VoteFlag.PreVote} or {VoteFlag.PreCommit}" +
                $"to create a valid signed vote.");
        }

        return new VoteMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _privateKey.Address,
            ValidatorPower = _validators.GetValidator(_privateKey.Address).Power,
            Flag = voteFlag,
        }.Sign(_privateKey);
    }

    private Maj23 MakeMaj23(int round, BlockHash blockHash, VoteFlag voteFlag)
    {
        if (voteFlag == VoteFlag.Null || voteFlag == VoteFlag.Unknown)
        {
            throw new ArgumentException(
                $"{nameof(voteFlag)} must be either {VoteFlag.PreVote} or {VoteFlag.PreCommit}" +
                $"to create a valid signed maj23.");
        }

        return new Maj23Metadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = _privateKey.Address,
            VoteFlag = voteFlag,
        }.Sign(_privateKey);
    }

    private (Block, int)? GetProposal()
        => Proposal is { } p && _proposalBlock is { } b ? (b, p.ValidRound) : null;
}
