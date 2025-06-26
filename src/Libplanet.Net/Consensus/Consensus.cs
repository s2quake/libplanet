using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public partial class Consensus(
    Blockchain blockchain,
    int height,
    ISigner signer,
    ImmutableSortedSet<Validator> validators,
    ConsensusOptions options)
    : IAsyncDisposable
{
    private readonly Subject<int> _roundStartedSubject = new();
    private readonly Subject<Exception> _exceptionOccurredSubject = new();
    private readonly Subject<ConsensusStep> _stepChangedSubject = new();
    private readonly Subject<Vote> _preVotedSubject = new();
    private readonly Subject<Vote> _preCommittedSubject = new();
    private readonly Subject<Maj23> _quorumReachedSubject = new();
    private readonly Subject<ProposalClaim> _proposalClaimedSubject = new();
    private readonly Subject<Proposal> _blockProposeSubject = new();
    private readonly Subject<(Block Block, BlockCommit BlockCommit)> _completedSubject = new();
    private readonly VoteContext _preVotes = new(height, VoteType.PreVote, validators);
    private readonly VoteContext _preCommits = new(height, VoteType.PreCommit, validators);
    private readonly ImmutableSortedSet<Validator> _validators = validators;
    private readonly HashSet<int> _hasTwoThirdsPreVoteTypes = [];
    private readonly HashSet<int> _preVoteTimeouts = [];
    private readonly HashSet<int> _preCommitTimeouts = [];
    private readonly HashSet<int> _preCommitWaits = [];
    private readonly HashSet<int> _endCommitWaits = [];
    private readonly ICache<BlockHash, bool> _blockValidationCache = new ConcurrentLruBuilder<BlockHash, bool>()
        .WithCapacity(128)
        .Build();

    private Dispatcher? _dispatcher;
    private CancellationTokenSource? _cancellationTokenSource;
    private Block? _lockedBlock;
    private int _lockedRound = -1;
    private Block? _validBlock;
    private int _validRound = -1;
    private Block? _decidedBlock;
    private bool _disposed;
    private ConsensusStep _step;

    public Consensus(Blockchain blockchain, int height, ISigner signer, ConsensusOptions options)
        : this(blockchain, height, signer, blockchain.GetValidators(height), options)
    {
    }

    public IObservable<int> RoundStarted => _roundStartedSubject;

    public IObservable<Exception> ExceptionOccurred => _exceptionOccurredSubject;

    public IObservable<ConsensusStep> StepChanged => _stepChangedSubject;

    public IObservable<Vote> PreVoteed => _preVotedSubject;

    public IObservable<Vote> PreCommitted => _preCommittedSubject;

    public IObservable<Maj23> QuorumReached => _quorumReachedSubject;

    public IObservable<ProposalClaim> ProposalClaimed => _proposalClaimedSubject;

    public IObservable<Proposal> BlockProposed => _blockProposeSubject;

    public IObservable<(Block Block, BlockCommit BlockCommit)> Completed => _completedSubject;

    public int Height { get; } = ValidateHeight(height);

    public int Round { get; private set; } = -1;

    public bool IsRunning { get; private set; }

    public ConsensusStep Step
    {
        get => _step;
        private set
        {
            if (_step != value)
            {
                _step = value;
                _stepChangedSubject.OnNext(value);
            }
        }
    }

    public Proposal? Proposal { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            if (_dispatcher is not null)
            {
                await _dispatcher.DisposeAsync();
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _dispatcher = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public BlockCommit GetBlockCommit()
    {
        try
        {
            return _preCommits[Round].ToBlockCommit();
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
            VoteType.PreVote => _preVotes[round].BitArrayByBlockHash(blockHash),
            VoteType.PreCommit => _preCommits[round].BitArrayByBlockHash(blockHash),
            _ => throw new ArgumentException("VoteType should be either PreVote or PreCommit.", nameof(voteType)),
        };

        return new VoteSetBitsMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            VoteType = voteType,
            VoteBits = [.. voteBits],
        }.Sign(signer);
    }

    public VoteSetBits? AddMaj23(Maj23 maj23)
    {
        if (maj23.VoteType is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new ArgumentException("VoteType should be either PreVote or PreCommit.", nameof(maj23));
        }

        var voteContext = maj23.VoteType == VoteType.PreVote ? _preVotes : _preCommits;
        if (voteContext.SetMaj23(maj23))
        {
            var voteSetBits = GetVoteSetBits(maj23.Round, maj23.BlockHash, maj23.VoteType);
            return voteSetBits.VoteBits.All(b => b) ? null : voteSetBits;
        }

        return null;
    }

    public IEnumerable<ConsensusMessage> GetVoteSetBitsResponse(VoteSetBits voteSetBits)
    {
        IEnumerable<Vote> votes;
        try
        {
            votes = voteSetBits.VoteType switch
            {
                VoteType.PreVote =>
                _preVotes[voteSetBits.Round].MappedList().Where(
                    (vote, index)
                    => !voteSetBits.VoteBits[index]
                    && vote is { }
                    && vote.Type == VoteType.PreVote).Select(vote => vote!),
                VoteType.PreCommit =>
                _preCommits[voteSetBits.Round].MappedList().Where(
                    (vote, index)
                    => !voteSetBits.VoteBits[index]
                    && vote is { }
                    && vote.Type == VoteType.PreCommit).Select(vote => vote!),
                _ => throw new ArgumentException("VoteType should be PreVote or PreCommit.", nameof(voteSetBits)),
            };
        }
        catch (KeyNotFoundException)
        {
            votes = [];
        }

        return votes.Select(GetMessage);

        static ConsensusMessage GetMessage(Vote vote) => vote.Type switch
        {
            VoteType.PreVote => new ConsensusPreVoteMessage { PreVote = vote },
            VoteType.PreCommit => new ConsensusPreCommitMessage { PreCommit = vote },
            _ => throw new ArgumentException("VoteType should be PreVote or PreCommit.", nameof(vote)),
        };
    }

    private Block GetValue() => blockchain.ProposeBlock(signer);

    private bool IsValid(Block block)
    {
        if (_blockValidationCache.TryGet(block.BlockHash, out var isValid))
        {
            return isValid;
        }
        else
        {
            if (block.Height != Height)
            {
                _blockValidationCache.AddOrUpdate(block.BlockHash, false);
                return false;
            }

            try
            {
                block.Validate(blockchain);
                blockchain.Options.BlockOptions.Validate(block);

                foreach (var tx in block.Transactions)
                {
                    blockchain.Options.TransactionOptions.Validate(tx);
                }
            }
            catch (Exception e) when (e is InvalidOperationException)
            {
                _blockValidationCache.AddOrUpdate(block.BlockHash, false);
                return false;
            }

            _blockValidationCache.AddOrUpdate(block.BlockHash, true);
            return true;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Consensus is already running.");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _dispatcher = new Dispatcher(this);
        _dispatcher.UnhandledException += Dispatcher_UnhandledException;
        await _dispatcher.InvokeAsync(_ =>
        {
            StartRound(0);
            ProcessGenericUponRules();
        }, _cancellationTokenSource.Token);
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning || _cancellationTokenSource is null || _dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        await _cancellationTokenSource.CancelAsync();
        _dispatcher.UnhandledException -= Dispatcher_UnhandledException;
        await _dispatcher.DisposeAsync();
        _dispatcher = null;

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;

        _lockedBlock = null;
        _lockedRound = -1;
        _validBlock = null;
        _validRound = -1;
        _decidedBlock = null;
        Round = -1;
        Step = ConsensusStep.Default;
        IsRunning = false;
    }

    public void PostProposal(Proposal proposal)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.Post(() =>
        {
            SetProposal(proposal);
            ProcessGenericUponRules();
        });
    }

    public void PostVote(Vote vote)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.Post(() =>
        {
            _preVotes.Add(vote);
            ProcessHeightOrRoundUponRules(vote);
            ProcessGenericUponRules();
        });
    }

    [Obsolete]
    internal void ProduceMessage(ConsensusMessage message)
    {
    }

    private void EnterPreCommitWait(int round, BlockHash blockHash)
    {
        if (_dispatcher is null || _cancellationTokenSource is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        if (!_preCommitWaits.Add(round))
        {
            return;
        }

        var delay = options.EnterPreCommitDelay;
        var cancellationToken = _cancellationTokenSource.Token;
        _ = _dispatcher.PostAfterAsync(Invoke, delay, cancellationToken);

        void Invoke(CancellationToken _)
        {
            EnterPreCommit(round, blockHash);
            ProcessGenericUponRules();
        }
    }

    private void EnterEndCommitWait(int round)
    {
        if (_dispatcher is null || _cancellationTokenSource is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        if (!_endCommitWaits.Add(round))
        {
            return;
        }

        var delay = options.EnterEndCommitDelay;
        var cancellationToken = _cancellationTokenSource.Token;

        _ = _dispatcher.PostAfterAsync(Invoke, delay, cancellationToken);

        void Invoke(CancellationToken _)
        {
            EnterEndCommit(round);
            ProcessGenericUponRules();
        }
    }

    private void PostProposeTimeout(int round)
    {
        if (_dispatcher is null || _cancellationTokenSource is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();

        var timeout = options.TimeoutPropose(round);
        var cancellationToken = _cancellationTokenSource.Token;
        _ = _dispatcher.PostAfterAsync(Invoke, timeout, cancellationToken);

        void Invoke(CancellationToken _)
        {
            if (round == Round && Step == ConsensusStep.Propose)
            {
                EnterPreVote(round, default);
                ProcessGenericUponRules();
            }
        }
    }

    private void PostPreVoteTimeout(int round)
    {
        if (_dispatcher is null || _cancellationTokenSource is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        if (_preCommitTimeouts.Contains(round) || !_preVoteTimeouts.Add(round))
        {
            return;
        }

        var timeout = options.TimeoutPreVote(round);
        var cancellationToken = _cancellationTokenSource.Token;
        _ = _dispatcher.PostAfterAsync(Invoke, timeout, cancellationToken);

        void Invoke(CancellationToken _)
        {
            if (round == Round && Step == ConsensusStep.PreVote)
            {
                EnterPreCommit(round, default);
                ProcessGenericUponRules();
            }
        }
    }

    private void PostPreCommitTimeout(int round)
    {
        if (_dispatcher is null || _cancellationTokenSource is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        if (!_preCommitTimeouts.Add(round))
        {
            return;
        }

        var timeout = options.TimeoutPreCommit(round);
        var cancellationToken = _cancellationTokenSource.Token;
        _ = _dispatcher.PostAfterAsync(Invoke, timeout, cancellationToken);

        void Invoke(CancellationToken _)
        {
            if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
            {
                return;
            }

            if (round == Round)
            {
                EnterEndCommit(round);
                ProcessGenericUponRules();
            }
        }
    }

    private void StartRound(int round)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();

        Round = round;
        _preVotes.Round = round;
        _preCommits.Round = round;
        Proposal = null;
        Step = ConsensusStep.Propose;
        _roundStartedSubject.OnNext(round);
        if (_validators.GetProposer(Height, Round).Address == signer.Address
            && (_validBlock ?? GetValue()) is Block proposalBlock)
        {
            var proposal = new ProposalBuilder
            {
                Block = proposalBlock,
                Round = Round,
                Timestamp = DateTimeOffset.UtcNow,
                ValidRound = _validRound,
            }.Create(signer);
            _blockProposeSubject.OnNext(proposal);
        }
        else
        {
            PostProposeTimeout(Round);
        }
    }

    private void SetProposal(Proposal proposal)
    {
        if (Proposal is not null)
        {
            throw new InvalidOperationException($"Proposal already exists for height {Height} and round {Round}");
        }

        if (!_validators.GetProposer(Height, Round).Address.Equals(proposal.Validator))
        {
            var message = $"Given proposal's proposer {proposal.Validator} does not match " +
                          $"with the current proposer for height {Height} and round {Round}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        if (proposal.Round != Round)
        {
            var message = $"Given proposal's round {proposal.Round} does not match " +
                          $"with the current round {Round}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        // Should check if +2/3 votes already collected and the proposal does not match
        if (_preVotes[Round].TwoThirdsMajority(out var preVoteMaj23) &&
            !proposal.BlockHash.Equals(preVoteMaj23))
        {
            var message = $"Given proposal's block hash {proposal.BlockHash} does not match " +
                          $"with the collected +2/3 preVotes' block hash {preVoteMaj23}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        if (_preVotes[Round].TwoThirdsMajority(out var preCommitMaj23) &&
            !proposal.BlockHash.Equals(preCommitMaj23))
        {
            var message = $"Given proposal's block hash {proposal.BlockHash} does not match " +
                          $"with the collected +2/3 preCommits' block hash {preCommitMaj23}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        Proposal = proposal;
    }

    private void ProcessGenericUponRules()
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        (Block Block, int ValidRound)? propose = GetProposal();
        if (Step == ConsensusStep.Propose && propose is { } p1 && p1.ValidRound == -1)
        {
            if (IsValid(p1.Block) && (_lockedRound == -1 || _lockedBlock == p1.Block))
            {
                EnterPreVote(Round, p1.Block.BlockHash);
            }
            else
            {
                EnterPreVote(Round, default);
            }
        }

        if (Step == ConsensusStep.Propose
            && propose is { } p2
            && p2.ValidRound >= 0
            && p2.ValidRound < Round
            && _preVotes[p2.ValidRound].TwoThirdsMajority(out BlockHash hash1)
            && hash1.Equals(p2.Block.BlockHash))
        {
            if (IsValid(p2.Block) && (_lockedRound <= p2.ValidRound || _lockedBlock == p2.Block))
            {
                EnterPreVote(Round, p2.Block.BlockHash);
            }
            else
            {
                EnterPreVote(Round, default);
            }
        }

        if (Step == ConsensusStep.PreVote && _preVotes[Round].HasTwoThirdsAny)
        {
            PostPreVoteTimeout(Round);
        }

        if ((Step == ConsensusStep.PreVote || Step == ConsensusStep.PreCommit)
            && propose is { } p3
            && _preVotes[Round].TwoThirdsMajority(out BlockHash hash2)
            && hash2.Equals(p3.Block.BlockHash)
            && IsValid(p3.Block)
            && !_hasTwoThirdsPreVoteTypes.Contains(Round))
        {
            _hasTwoThirdsPreVoteTypes.Add(Round);
            if (Step == ConsensusStep.PreVote)
            {
                _lockedBlock = p3.Block;
                _lockedRound = Round;
                EnterPreCommitWait(Round, p3.Block.BlockHash);

                var maj23 = new Maj23Metadata
                {
                    Height = Height,
                    Round = Round,
                    BlockHash = p3.Block.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = signer.Address,
                    VoteType = VoteType.PreVote,
                }.Sign(signer);
                _quorumReachedSubject.OnNext(maj23);
            }

            _validBlock = p3.Block;
            _validRound = Round;
        }

        if (Step == ConsensusStep.PreVote && _preVotes[Round].TwoThirdsMajority(out BlockHash hash3))
        {
            if (hash3.Equals(default))
            {
                EnterPreCommitWait(Round, default);
            }
            else if (Proposal is { } proposal && !proposal.BlockHash.Equals(hash3))
            {
                // +2/3 votes were collected and is not equal to proposal's,
                // remove invalid proposal.
                Proposal = null;

                var proposalClaim = new ProposalClaimMetadata
                {
                    Height = Height,
                    Round = Round,
                    BlockHash = hash3,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = signer.Address,
                }.Sign(signer);
                _proposalClaimedSubject.OnNext(proposalClaim);
            }
        }

        if (_preCommits[Round].HasTwoThirdsAny)
        {
            PostPreCommitTimeout(Round);
        }
    }

    private void ProcessHeightOrRoundUponRules(Vote vote)
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        var round = vote.Round;
        if (GetProposal() is (Block block4, _) &&
            _preCommits[Round].TwoThirdsMajority(out BlockHash hash) &&
            block4.BlockHash.Equals(hash) &&
            IsValid(block4))
        {
            _decidedBlock = block4;

            // Maybe need to broadcast periodically?
            var maj23 = new Maj23Metadata
            {
                Height = Height,
                Round = round,
                BlockHash = block4.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                VoteType = VoteType.PreCommit,
            }.Sign(signer);
            _quorumReachedSubject.OnNext(maj23);
            EnterEndCommitWait(Round);
            return;
        }

        if (round > Round && _preVotes[round].HasOneThirdsAny)
        {
            StartRound(round);
        }
    }

    private void EnterPreVote(int round, BlockHash blockHash)
    {
        if (Round != round || Step >= ConsensusStep.PreVote)
        {
            return;
        }

        var vote = new VoteMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            ValidatorPower = _validators.GetValidator(signer.Address).Power,
            Type = VoteType.PreVote,
        }.Sign(signer);
        Step = ConsensusStep.PreVote;
        _preVotedSubject.OnNext(vote);
    }

    private void EnterPreCommit(int round, BlockHash blockHash)
    {
        if (Round != round || Step >= ConsensusStep.PreCommit)
        {
            return;
        }

        var vote = new VoteMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            ValidatorPower = _validators.GetValidator(signer.Address).Power,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        Step = ConsensusStep.PreCommit;
        _preCommittedSubject.OnNext(vote);
    }

    private void EnterEndCommit(int round)
    {
        if (Round != round || Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        Step = ConsensusStep.EndCommit;
        if (_decidedBlock is not { } block)
        {
            StartRound(Round + 1);
            return;
        }

        try
        {
            IsValid(block);
            _completedSubject.OnNext((block, GetBlockCommit()));
        }
        catch (Exception e)
        {
            _exceptionOccurredSubject.OnNext(e);
            return;
        }
    }

    private static int ValidateHeight(int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return height;
    }

    private void Dispatcher_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        => _exceptionOccurredSubject.OnNext((Exception)e.ExceptionObject);

    private (Block, int)? GetProposal() => Proposal is { } p ? (p.Block, p.ValidRound) : null;
}
