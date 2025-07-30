using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using Caching;
using Libplanet.Blockchain;
using Libplanet.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Serilog;

namespace Libplanet.Net.Consensus;

public partial class Context : IDisposable
{
    private readonly ContextOption _contextOption;

    private readonly BlockChain _blockChain;
    private readonly Codec _codec;
    private readonly ImmutableSortedSet<Validator> _validatorSet;
    private readonly Channel<ConsensusMsg> _messageRequests;
    private readonly Channel<System.Action> _mutationRequests;
    private readonly HeightVoteSet _heightVoteSet;
    private readonly PrivateKey _privateKey;
    private readonly HashSet<int> _hasTwoThirdsPreVoteFlags;
    private readonly HashSet<int> _preVoteTimeoutFlags;
    private readonly HashSet<int> _preCommitTimeoutFlags;
    private readonly HashSet<int> _preCommitWaitFlags;
    private readonly HashSet<int> _endCommitWaitFlags;
    private readonly EvidenceExceptionCollector _evidenceCollector
        = new EvidenceExceptionCollector();

    private readonly CancellationTokenSource _cancellationTokenSource;

    private readonly ILogger _logger;
    private readonly LRUCache<BlockHash, bool> _blockValidationCache;

    private Proposal? _proposal;
    private Block? _proposalBlock;
    private Block? _lockedValue;
    private int _lockedRound;
    private Block? _validValue;
    private int _validRound;
    private Block? _decision;
    private int _committedRound;
    private readonly BlockCommit _lastCommit;

    public Context(
        BlockChain blockChain,
        int height,
        BlockCommit lastCommit,
        PrivateKey privateKey,
        ImmutableSortedSet<Validator> validators,
        ContextOption contextOption)
        : this(
            blockChain,
            height,
            lastCommit,
            privateKey,
            validators,
            ConsensusStep.Default,
            -1,
            128,
            contextOption)
    {
    }

    private Context(
        BlockChain blockChain,
        int height,
        BlockCommit lastCommit,
        PrivateKey privateKey,
        ImmutableSortedSet<Validator> validators,
        ConsensusStep consensusStep,
        int round = -1,
        int cacheSize = 128,
        ContextOption? contextOption = null)
    {
        if (height < 1)
        {
            throw new ArgumentException(
                $"Given {nameof(height)} must be positive: {height}", nameof(height));
        }

        _logger = Log
            .ForContext("Tag", "Consensus")
            .ForContext("SubTag", "Context")
            .ForContext<Context>()
            .ForContext("Source", nameof(Context));

        _privateKey = privateKey;
        Height = height;
        Round = round;
        Step = consensusStep;
        _lastCommit = lastCommit;
        _lockedValue = null;
        _lockedRound = -1;
        _validValue = null;
        _validRound = -1;
        _decision = null;
        _committedRound = -1;
        _blockChain = blockChain;
        _codec = new Codec();
        _messageRequests = Channel.CreateUnbounded<ConsensusMsg>();
        _mutationRequests = Channel.CreateUnbounded<System.Action>();
        _heightVoteSet = new HeightVoteSet(height, validators);
        _hasTwoThirdsPreVoteFlags = new HashSet<int>();
        _preVoteTimeoutFlags = new HashSet<int>();
        _preCommitTimeoutFlags = new HashSet<int>();
        _preCommitWaitFlags = new HashSet<int>();
        _endCommitWaitFlags = new HashSet<int>();
        _validatorSet = validators;
        _cancellationTokenSource = new CancellationTokenSource();
        _blockValidationCache =
            new LRUCache<BlockHash, bool>(cacheSize, Math.Max(cacheSize / 64, 8));

        _contextOption = contextOption ?? new ContextOption();

        _logger.Information(
            "Created Context for height #{Height}, round #{Round}",
            Height,
            Round);
    }

    /// <summary>
    /// A target height of this consensus state. This is also a block height now in consensus.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// A round represents of this consensus state.
    /// </summary>
    public int Round { get; private set; }

    /// <summary>
    /// A step represents of this consensus state. See <see cref="Context"/> for more detail.
    /// </summary>
    public ConsensusStep Step { get; private set; }

    public Proposal? Proposal
    {
        get => _proposal;
        private set
        {
            if (value is { } p)
            {
                _proposal = p;
                _proposalBlock = ModelSerializer.DeserializeFromBytes<Block>(p.MarshaledBlock);
            }
            else
            {
                _proposal = null;
                _proposalBlock = null;
            }
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose()"/>
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _messageRequests.Writer.TryComplete();
        _mutationRequests.Writer.TryComplete();
    }

    /// <summary>
    /// Returns a <see cref="BlockCommit"/> if the context is committed.
    /// </summary>
    /// <returns>Returns <see cref="BlockCommit"/> if the context is committed
    /// otherwise returns <see langword="null"/>.
    /// </returns>
    public BlockCommit GetBlockCommit()
    {
        try
        {
            var blockCommit = _heightVoteSet.PreCommits(Round).ToBlockCommit();
            _logger.Debug(
                "{FName}: CommittedRound: {CommittedRound}, Decision: {Decision}, " +
                "BlockCommit: {BlockCommit}",
                nameof(GetBlockCommit),
                _committedRound,
                _decision,
                blockCommit);
            return blockCommit;
        }
        catch (KeyNotFoundException)
        {
            return BlockCommit.Empty;
        }
    }

    public VoteSetBits GetVoteSetBits(int round, BlockHash blockHash, VoteFlag flag)
    {
        // If executed in correct manner (called by Maj23),
        // _heightVoteSet.PreVotes(round) on below cannot throw KeyNotFoundException,
        // since RoundVoteSet has been already created on SetPeerMaj23.
        bool[] voteBits = flag switch
        {
            VoteFlag.PreVote => _heightVoteSet.PreVotes(round).BitArrayByBlockHash(blockHash),
            VoteFlag.PreCommit
                => _heightVoteSet.PreCommits(round).BitArrayByBlockHash(blockHash),
            _ => throw new ArgumentException(
                "VoteFlag should be either PreVote or PreCommit.",
                nameof(flag)),
        };

        return new VoteSetBitsMetadata(
            Height,
            round,
            blockHash,
            DateTimeOffset.UtcNow,
            _privateKey.PublicKey,
            flag,
            voteBits).Sign(_privateKey);
    }

    /// <summary>
    /// Add a <see cref="ConsensusMsg"/> to the context.
    /// </summary>
    /// <param name="maj23">A <see cref="ConsensusMsg"/> to add.</param>
    /// <returns>A <see cref="VoteSetBits"/> if given <paramref name="maj23"/> is valid and
    /// required.</returns>
    public VoteSetBits? AddMaj23(Maj23 maj23)
    {
        try
        {
            if (_heightVoteSet.SetPeerMaj23(maj23))
            {
                var voteSetBits = GetVoteSetBits(maj23.Round, maj23.BlockHash, maj23.Flag);
                return voteSetBits.VoteBits.All(b => b) ? null : voteSetBits;
            }

            return null;
        }
        catch (InvalidMaj23Exception ime)
        {
            var msg = $"Failed to add invalid maj23 {ime} to the " +
                      $"{nameof(HeightVoteSet)}";
            _logger.Error(ime, msg);
            ExceptionOccurred?.Invoke(this, ime);
            return null;
        }
    }

    public IEnumerable<ConsensusMsg> GetVoteSetBitsResponse(VoteSetBits voteSetBits)
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
                   VoteFlag.PreVote => (ConsensusMsg)new ConsensusPreVoteMsg(vote),
                   VoteFlag.PreCommit => (ConsensusMsg)new ConsensusPreCommitMsg(vote),
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
            { "number_of_validators", _validatorSet.Count },
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

    /// <summary>
    /// Collects <see cref="EvidenceException"/>s that are occurred during the consensus.
    /// </summary>
    /// <returns>A list of <see cref="EvidenceException"/>s.</returns>
    public EvidenceException[] CollectEvidenceExceptions() => _evidenceCollector.Flush();

    /// <summary>
    /// Gets the timeout of <see cref="ConsensusStep.PreVote"/> with the given
    /// round.
    /// </summary>
    /// <param name="round">A round to get the timeout.</param>
    /// <returns>A duration in <see cref="TimeSpan"/>.</returns>
    private TimeSpan TimeoutPreVote(long round)
    {
        return TimeSpan.FromMilliseconds(
            _contextOption.PreVoteTimeoutBase +
            (round * _contextOption.PreVoteTimeoutDelta));
    }

    /// <summary>
    /// Gets the timeout of <see cref="ConsensusStep.PreCommit"/> with the given
    /// round.
    /// </summary>
    /// <param name="round">A round to get the timeout.</param>
    /// <returns>A duration in <see cref="TimeSpan"/>.</returns>
    private TimeSpan TimeoutPreCommit(long round)
    {
        return TimeSpan.FromMilliseconds(
            _contextOption.PreCommitTimeoutBase +
            (round * _contextOption.PreCommitTimeoutDelta));
    }

    /// <summary>
    /// Gets the timeout of <see cref="ConsensusStep.Propose"/> with the given
    /// round.
    /// </summary>
    /// <param name="round">A round to get the timeout.</param>
    /// <returns>A duration in <see cref="TimeSpan"/>.</returns>
    private TimeSpan TimeoutPropose(long round)
    {
        return TimeSpan.FromMilliseconds(
            _contextOption.ProposeTimeoutBase +
            (round * _contextOption.ProposeTimeoutDelta));
    }

    /// <summary>
    /// Creates a new <see cref="Block"/> to propose.
    /// </summary>
    /// <returns>A new <see cref="Block"/> if successfully proposed,
    /// otherwise <see langword="null"/>.</returns>
    private Block? GetValue()
    {
        try
        {
            var evidence = _blockChain.PendingEvidences;
            Block block = _blockChain.ProposeBlock(_privateKey, _lastCommit, [.. evidence.Values]);
            _blockChain.Store.PutBlock(block);
            return block;
        }
        catch (Exception e)
        {
            _logger.Error(
                e,
                "Could not propose a block for height {Height} and round {Round}",
                Height,
                Round);
            ExceptionOccurred?.Invoke(this, e);
            return null;
        }
    }

    /// <summary>
    /// Publish <see cref="ConsensusMsg"/> to validators.
    /// </summary>
    /// <param name="message">A <see cref="ConsensusMsg"/> to publish.</param>
    /// <remarks><see cref="ConsensusMsg"/> should be published to itself.</remarks>
    private void PublishMessage(ConsensusMsg message) =>
        MessageToPublish?.Invoke(this, message);

    /// <summary>
    /// Validates the given block.
    /// </summary>
    /// <param name="block">A <see cref="Block"/> to validate.</param>
    /// <returns><see langword="true"/> if block is valid, otherwise <see langword="false"/>.
    /// </returns>
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
            _blockChain._rwlock.EnterUpgradeableReadLock();

            if (block.Height != Height)
            {
                _blockValidationCache.AddReplace(block.BlockHash, false);
                return false;
            }

            try
            {
                _blockChain.ValidateBlock(block);
                _blockChain.ValidateBlockNonces(
                    block.Transactions
                        .Select(tx => tx.Signer)
                        .Distinct()
                        .ToDictionary(
                            signer => signer,
                            signer => _blockChain.Store.GetTxNonce(
                                _blockChain.Id, signer)),
                    block);

                _blockChain.Options.BlockValidation(_blockChain, block);

                foreach (var tx in block.Transactions)
                {
                    _blockChain.Options.ValidateTransaction(_blockChain, tx);
                }

                _blockChain.ValidateBlockStateRootHash(block);
            }
            catch (Exception e) when (
                e is InvalidOperationException)
            {
                _logger.Debug(
                    e,
                    "Block #{Index} {Hash} is invalid",
                    block.Height,
                    block.BlockHash);
                _blockValidationCache.AddReplace(block.BlockHash, false);
                return false;
            }
            finally
            {
                _blockChain._rwlock.ExitUpgradeableReadLock();
            }

            _blockValidationCache.AddReplace(block.BlockHash, true);
            return true;
        }
    }

    /// <summary>
    /// Creates a signed <see cref="Vote"/> for a <see cref="ConsensusPreVoteMsg"/> or
    /// a <see cref="ConsensusPreCommitMsg"/>.
    /// </summary>
    /// <param name="round">Current context round.</param>
    /// <param name="hash">Current context locked <see cref="BlockHash"/>.</param>
    /// <param name="flag"><see cref="VoteFlag"/> of <see cref="Vote"/> to create.
    /// Set to <see cref="VoteFlag.PreVote"/> if message is <see cref="ConsensusPreVoteMsg"/>.
    /// If message is <see cref="ConsensusPreCommitMsg"/>, Set to
    /// <see cref="VoteFlag.PreCommit"/>.</param>
    /// <returns>Returns a signed <see cref="Vote"/> with consensus private key.</returns>
    /// <exception cref="ArgumentException">If <paramref name="flag"/> is either
    /// <see cref="VoteFlag.Null"/> or <see cref="VoteFlag.Unknown"/>.</exception>
    private Vote MakeVote(int round, BlockHash hash, VoteFlag flag)
    {
        if (flag == VoteFlag.Null || flag == VoteFlag.Unknown)
        {
            throw new ArgumentException(
                $"{nameof(flag)} must be either {VoteFlag.PreVote} or {VoteFlag.PreCommit}" +
                $"to create a valid signed vote.");
        }

        return new VoteMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = hash,
            Timestamp = DateTimeOffset.UtcNow,
            ValidatorPublicKey = _privateKey.PublicKey,
            ValidatorPower = _validatorSet.GetValidator(_privateKey.PublicKey).Power,
            Flag = flag,
        }.Sign(_privateKey);
    }

    /// <summary>
    /// Creates a signed <see cref="Maj23"/> for a <see cref="ConsensusMaj23Msg"/>.
    /// </summary>
    /// <param name="round">Current context round.</param>
    /// <param name="hash">Current context locked <see cref="BlockHash"/>.</param>
    /// <param name="flag"><see cref="VoteFlag"/> of <see cref="Maj23"/> to create.
    /// Set to <see cref="VoteFlag.PreVote"/> if +2/3 <see cref="ConsensusPreVoteMsg"/>
    /// messages that votes to the same block with proposal are collected.
    /// If +2/3 <see cref="ConsensusPreCommitMsg"/> messages that votes to the same block
    /// with proposal are collected, Set to <see cref="VoteFlag.PreCommit"/>.</param>
    /// <returns>Returns a signed <see cref="Maj23"/> with consensus private key.</returns>
    /// <exception cref="ArgumentException">If <paramref name="flag"/> is either
    /// <see cref="VoteFlag.Null"/> or <see cref="VoteFlag.Unknown"/>.</exception>
    private Maj23 MakeMaj23(int round, BlockHash hash, VoteFlag flag)
    {
        if (flag == VoteFlag.Null || flag == VoteFlag.Unknown)
        {
            throw new ArgumentException(
                $"{nameof(flag)} must be either {VoteFlag.PreVote} or {VoteFlag.PreCommit}" +
                $"to create a valid signed maj23.");
        }

        return new Maj23Metadata(
            Height,
            round,
            hash,
            DateTimeOffset.UtcNow,
            _privateKey.PublicKey,
            flag).Sign(_privateKey);
    }

    /// <summary>
    /// Gets the proposed block and valid round of the given round.
    /// </summary>
    /// <returns>Returns a tuple of proposer and valid round.  If proposal for the round
    /// does not exist, returns <see langword="null"/> instead.
    /// </returns>
    private (Block, int)? GetProposal() =>
        Proposal is { } p && _proposalBlock is { } b ? (b, p.ValidRound) : null;
}
