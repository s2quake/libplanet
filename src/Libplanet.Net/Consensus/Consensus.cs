using System.Reactive.Subjects;
using Libplanet.Net.Threading;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class Consensus(int height, ImmutableSortedSet<Validator> validators, ConsensusOptions options)
    : ServiceBase
{
    private readonly Subject<Round> _roundChangedSubject = new();
    private readonly Subject<Exception> _exceptionOccurredSubject = new();
    private readonly Subject<(Round, ConsensusStep)> _timeoutOccurredSubject = new();
    private readonly Subject<(Round, ConsensusStep, BlockHash BlockHash)> _stepChangedSubject = new();
    private readonly Subject<Proposal> _proposedSubject = new();
    private readonly Subject<(Proposal, BlockHash)> _proposalRejectedSubject = new();
    private readonly Subject<(Block, VoteType)> _quorumReachedSubject = new();
    private readonly Subject<(Block, BlockCommit)> _completedSubject = new();
    private readonly Subject<Vote> _preVotedSubject = new();
    private readonly Subject<Vote> _preCommittedSubject = new();

    private readonly Dictionary<BlockHash, bool> _blockValidationCache = [];
    private readonly RoundCollection _rounds = new(height, validators);

    private Dispatcher? _dispatcher;
    private Block? _lockedBlock;
    private int _lockedRound = -1;
    private Block? _validBlock;
    private int _validRound = -1;
    private Block? _decidedBlock;
    private Round? _round;

    public IObservable<Round> RoundChanged => _roundChangedSubject;

    public IObservable<Exception> ExceptionOccurred => _exceptionOccurredSubject;

    public IObservable<(Round Round, ConsensusStep Step)> TimeoutOccurred => _timeoutOccurredSubject;

    public IObservable<(Round Round, ConsensusStep Step, BlockHash BlockHash)> StepChanged => _stepChangedSubject;

    public IObservable<Proposal> Proposed => _proposedSubject;

    public IObservable<(Proposal Proposal, BlockHash BlockHash)> ProposalRejected => _proposalRejectedSubject;

    public IObservable<(Block Block, VoteType VoteType)> QuorumReached => _quorumReachedSubject;

    public IObservable<Vote> PreVoted => _preVotedSubject;

    public IObservable<Vote> PreCommitted => _preCommittedSubject;

    public IObservable<(Block Block, BlockCommit BlockCommit)> Completed => _completedSubject;

    public int Height { get; } = ValidateHeight(height);

    public Round Round
    {
        get => _round ?? throw new InvalidOperationException("Round is not set.");
        private set
        {
            if (_round != value)
            {
                _round = value;
                _roundChangedSubject.OnNext(value);
            }
        }
    }

    public ConsensusStep Step { get; private set; }

    public Proposal? Proposal { get; private set; }

    public ImmutableSortedSet<Validator> Validators => validators;

    public BlockCommit GetBlockCommit() => Round.PreCommits.GetBlockCommit();

    public bool AddMaj23(Maj23 maj23)
    {
        if (maj23.VoteType is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new ArgumentException("VoteType should be either PreVote or PreCommit.", nameof(maj23));
        }

        var round = _rounds[maj23.Round];
        var majorities = maj23.VoteType == VoteType.PreVote ? round.PreVoteMaj23s : round.PreCommitMaj23s;
        if (!majorities.ContainsKey(maj23.Validator))
        {
            majorities.Add(maj23);
            return true;
        }

        return false;
    }

    public VoteSetBits GetVoteSetBits(ISigner signer, int round, BlockHash blockHash, VoteType voteType)
    {
        var votes = voteType == VoteType.PreVote ? _rounds[round].PreVotes : _rounds[round].PreCommits;
        var voteBits = votes.GetVoteBits(blockHash);
        return new VoteSetBitsMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            VoteType = voteType,
            VoteBits = voteBits,
        }.Sign(signer);
    }

    public VoteSetBits GetVoteSetBits(ISigner signer, Maj23 maj23)
    {
        if (maj23.Height != Height)
        {
            throw new ArgumentException(
                $"Maj23 height {maj23.Height} does not match expected height {Height}.", nameof(maj23));
        }

        if (maj23.Round < 0 || maj23.Round >= _rounds.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(maj23), "Round is out of range.");
        }

        var round = _rounds[maj23.Round];
        var votes = maj23.VoteType == VoteType.PreVote ? round.PreVotes : round.PreCommits;

        var voteBits = votes.GetVoteBits(maj23.BlockHash);
        return new VoteSetBitsMetadata
        {
            Height = Height,
            Round = maj23.Round,
            BlockHash = maj23.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = maj23.Validator,
            VoteType = maj23.VoteType,
            VoteBits = [.. voteBits],
        }.Sign(signer);
    }

    public ImmutableArray<Vote> GetVotes(VoteSetBits voteSetBits)
    {
        if (voteSetBits.Height != Height)
        {
            throw new ArgumentException(
                $"VoteSetBits height {voteSetBits.Height} does not match expected height {Height}.",
                nameof(voteSetBits));
        }

        if (voteSetBits.VoteType is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new ArgumentException("VoteType should be either PreVote or PreCommit.", nameof(voteSetBits));
        }

        if (voteSetBits.VoteBits.Length != validators.Count)
        {
            throw new ArgumentException(
                $"VoteBits length {voteSetBits.VoteBits.Length} does not match validators count {validators.Count}.",
                nameof(voteSetBits));
        }

        var round = _rounds[voteSetBits.Round];
        var voteBits = voteSetBits.VoteBits;
        var votes = voteSetBits.VoteType is VoteType.PreVote ? round.PreVotes : round.PreCommits;

        var voteList = new List<Vote>(Validators.Count);
        for (var i = 0; i < voteBits.Length; i++)
        {
            if (!voteBits[i] && votes.TryGetValue(Validators[i].Address, out var vote))
            {
                voteList.Add(vote);
            }
        }

        return [.. voteList];
    }

    public void Propose(Proposal proposal)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
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

    public void PreVote(Vote vote)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        if (vote.Height != Height)
        {
            throw new ArgumentException(
                $"Vote height {vote.Height} does not match expected height {Height}.", nameof(vote));
        }

        if (vote.Type is not VoteType.PreVote)
        {
            throw new ArgumentException("Vote type must be PreVote.", nameof(vote));
        }

        if (vote.Round < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vote), "Round must be non-negative.");
        }

        _dispatcher.Post(() =>
        {
            var round = _rounds[vote.Round];
            var votes = round.PreVotes;
            votes.Add(vote);
            _preVotedSubject.OnNext(vote);
            ProcessHeightOrRoundUponRules(vote);
            ProcessGenericUponRules();
        });
    }

    public void PreCommit(Vote vote)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        if (vote.Height != Height)
        {
            throw new ArgumentException(
                $"Vote height {vote.Height} does not match expected height {Height}.", nameof(vote));
        }

        if (vote.Type is not VoteType.PreCommit)
        {
            throw new ArgumentException("Vote type must be PreCommit.", nameof(vote));
        }

        if (vote.Round < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vote), "Round must be non-negative.");
        }

        _dispatcher.Post(() =>
        {
            var votes = Round.PreCommits;
            votes.Add(vote);
            _preCommittedSubject.OnNext(vote);
            ProcessHeightOrRoundUponRules(vote);
            ProcessGenericUponRules();
        });
    }

    internal bool IsProposer(Address address)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        return Validators.GetProposer(Height, Round.Index).Address == address;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _dispatcher = new Dispatcher(this);
        _dispatcher.UnhandledException += Dispatcher_UnhandledException;
        await _dispatcher.InvokeAsync(_ =>
        {
            StartRound(0);
            ProcessGenericUponRules();
        }, cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        if (_dispatcher is not null)
        {
            _dispatcher.UnhandledException -= Dispatcher_UnhandledException;
            await _dispatcher.DisposeAsync();
            _dispatcher = null;
        }

        _lockedBlock = null;
        _lockedRound = -1;
        _validBlock = null;
        _validRound = -1;
        _decidedBlock = null;
        _round = null;
        Step = ConsensusStep.Default;
        _stepChangedSubject.OnNext((Round, Step, default));
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (_dispatcher is not null)
        {
            await _dispatcher.DisposeAsync();
            _dispatcher = null;
        }

        _roundChangedSubject.Dispose();
        _exceptionOccurredSubject.Dispose();
        _stepChangedSubject.Dispose();
        _proposedSubject.Dispose();
        _completedSubject.Dispose();
        _preVotedSubject.Dispose();
        await base.DisposeAsyncCore();
    }

    private static int ValidateHeight(int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return height;
    }

    private bool IsValid(Block block)
    {
        if (_blockValidationCache.TryGetValue(block.BlockHash, out var isValid))
        {
            return isValid;
        }
        else
        {
            if (block.Height != Height)
            {
                _blockValidationCache[block.BlockHash] = false;
                return false;
            }

            try
            {
                options.ValidateBlock(block);
            }
            catch (Exception e) when (e is InvalidOperationException)
            {
                _blockValidationCache[block.BlockHash] = false;
                return false;
            }

            _blockValidationCache[block.BlockHash] = true;
            return true;
        }
    }

    private void EnterPreCommitWait(Round round, BlockHash blockHash)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();

        if (round.IsPreCommitWaitScheduled)
        {
            return;
        }

        var delay = options.EnterPreCommitDelay;
        _ = _dispatcher.PostAfterAsync(Invoke, delay, default);

        void Invoke(CancellationToken _)
        {
            EnterPreCommitStep(round, blockHash);
            ProcessGenericUponRules();
        }
    }

    private void EnterEndCommitWait(Round round)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        if (!round.TrySetEndCommitWait())
        {
            return;
        }

        var delay = options.EnterEndCommitDelay;
        _ = _dispatcher.PostAfterAsync(Invoke, delay, default);

        void Invoke(CancellationToken _)
        {
            EnterEndCommitStep(round);
            ProcessGenericUponRules();
        }
    }

    private void PostProposeTimeout(Round round)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();

        var timeout = options.TimeoutPropose(round);
        _ = _dispatcher.PostAfterAsync(Invoke, timeout, default);

        void Invoke(CancellationToken _)
        {
            if (round == _round && Step == ConsensusStep.Propose)
            {
                _timeoutOccurredSubject.OnNext((round, ConsensusStep.Propose));
                if (Proposal is null)
                {
                    StartRound(round.Index + 1);
                }
                else
                {
                    EnterPreVoteStep(round, default);
                    ProcessGenericUponRules();
                }
            }
        }
    }

    private void PostPreVoteTimeout(Round round)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();

        if (round.TrySetPreVoteTimeout())
        {
            var timeout = options.TimeoutPreVote(round);
            _ = _dispatcher.PostAfterAsync(Invoke, timeout, default);
        }

        void Invoke(CancellationToken _)
        {
            if (round == _round && Step == ConsensusStep.PreVote)
            {
                EnterPreCommitStep(round, default);
                _timeoutOccurredSubject.OnNext((round, ConsensusStep.PreVote));
                ProcessGenericUponRules();
            }
        }
    }

    private void PostPreCommitTimeout(Round round)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        if (round.TrySetPreCommitTimeout())
        {
            var timeout = options.TimeoutPreCommit(round);
            _ = _dispatcher.PostAfterAsync(Invoke, timeout, StoppingToken);
        }

        void Invoke(CancellationToken _)
        {
            if (round == _round && Step is ConsensusStep.PreVote or ConsensusStep.PreCommit)
            {
                EnterEndCommitStep(round);
                _timeoutOccurredSubject.OnNext((round, ConsensusStep.PreCommit));
                ProcessGenericUponRules();
            }
        }
    }

    private void StartRound(int roundIndex)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();

        Round = _rounds[roundIndex];
        Proposal = null;
        Step = ConsensusStep.Propose;

        _roundChangedSubject.OnNext(Round);
        _stepChangedSubject.OnNext((Round, Step, default));

        PostProposeTimeout(Round);
    }

    private void SetProposal(Proposal proposal)
    {
        if (Proposal is not null)
        {
            throw new InvalidOperationException($"Proposal already exists for height {Height} and round {Round}");
        }

        var roundIndex = Round.Index;

        if (!Validators.GetProposer(Height, roundIndex).Address.Equals(proposal.Validator))
        {
            var message = $"Given proposal's proposer {proposal.Validator} does not match " +
                          $"with the current proposer for height {Height} and round {roundIndex}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        if (proposal.Round != roundIndex)
        {
            var message = $"Given proposal's round {proposal.Round} does not match " +
                          $"with the current round {roundIndex}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        // Should check if +2/3 votes already collected and the proposal does not match
        if (_rounds[roundIndex].PreVotes.BlockHash != proposal.BlockHash)
        {
            var message = $"Given proposal's block hash {proposal.BlockHash} does not match " +
                          $"with the collected +2/3 preVotes' block hash {_rounds[roundIndex].PreVotes.BlockHash}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        if (_rounds[roundIndex].PreCommits.BlockHash != proposal.BlockHash)
        {
            var message = $"Given proposal's block hash {proposal.BlockHash} does not match " +
                          $"with the collected +2/3 preCommits' block hash {_rounds[roundIndex].PreCommits.BlockHash}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        Proposal = proposal;
        _proposedSubject.OnNext(proposal);
    }

    private void ProcessGenericUponRules()
    {
        if (Step is ConsensusStep.Default or ConsensusStep.EndCommit)
        {
            return;
        }

        var round = Round;
        var roundIndex = round.Index;

        (Block Block, int ValidRound)? propose = GetProposal();
        if (Step == ConsensusStep.Propose && propose is { } p1 && p1.ValidRound == -1)
        {
            if (IsValid(p1.Block) && (_lockedRound == -1 || _lockedBlock == p1.Block))
            {
                EnterPreVoteStep(Round, p1.Block.BlockHash);
            }
            else
            {
                EnterPreVoteStep(Round, default);
            }
        }

        if (Step == ConsensusStep.Propose
            && propose is { } p2
            && p2.ValidRound >= 0
            && p2.ValidRound < roundIndex
            && _rounds[p2.ValidRound].PreVotes.BlockHash == p2.Block.BlockHash)
        {
            if (IsValid(p2.Block) && (_lockedRound <= p2.ValidRound || _lockedBlock == p2.Block))
            {
                EnterPreVoteStep(Round, p2.Block.BlockHash);
            }
            else
            {
                EnterPreVoteStep(Round, default);
            }
        }

        if (Step == ConsensusStep.PreVote && round.PreVotes.HasTwoThirdsAny)
        {
            PostPreVoteTimeout(round);
        }

        if ((Step == ConsensusStep.PreVote || Step == ConsensusStep.PreCommit)
            && propose is { } p3
            && round.PreVotes.BlockHash == p3.Block.BlockHash
            && IsValid(p3.Block)
            && !round.HasTwoThirdsPreVoteTypes)
        {
            round.HasTwoThirdsPreVoteTypes = true;
            if (Step == ConsensusStep.PreVote)
            {
                _lockedBlock = p3.Block;
                _lockedRound = roundIndex;
                EnterPreCommitWait(round, p3.Block.BlockHash);

                _quorumReachedSubject.OnNext((p3.Block, VoteType.PreVote));
            }

            _validBlock = p3.Block;
            _validRound = Round.Index;
        }

        if (Step == ConsensusStep.PreVote)
        {
            var hash3 = round.PreVotes.BlockHash;
            if (hash3 == default)
            {
                EnterPreCommitWait(round, default);
            }
            else if (Proposal is { } proposal && !proposal.BlockHash.Equals(hash3))
            {
                Proposal = null;
                _proposalRejectedSubject.OnNext((proposal, hash3));
            }
        }

        if (round.PreCommits.HasTwoThirdsAny)
        {
            PostPreCommitTimeout(round);
        }
    }

    private void ProcessHeightOrRoundUponRules(Vote vote)
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        var round = _rounds[vote.Round];
        var roundIndex = round.Index;
        if (GetProposal() is (Block block4, _)
            && round.PreCommits.BlockHash == block4.BlockHash
            && IsValid(block4))
        {
            _decidedBlock = block4;
            _quorumReachedSubject.OnNext((block4, VoteType.PreCommit));
            EnterEndCommitWait(Round);
            return;
        }

        if (roundIndex > Round.Index && round.PreVotes.HasOneThirdsAny)
        {
            StartRound(roundIndex);
        }
    }

    private void EnterPreVoteStep(Round round, BlockHash blockHash)
    {
        if (round != _round || Step >= ConsensusStep.PreVote)
        {
            return;
        }

        Step = ConsensusStep.PreVote;
        _stepChangedSubject.OnNext((round, Step, blockHash));
    }

    private void EnterPreCommitStep(Round round, BlockHash blockHash)
    {
        if (_round != round || Step >= ConsensusStep.PreCommit)
        {
            return;
        }

        Step = ConsensusStep.PreCommit;
        _stepChangedSubject.OnNext((round, Step, blockHash));
    }

    private void EnterEndCommitStep(Round round)
    {
        if (round != _round || Step is ConsensusStep.Default or ConsensusStep.EndCommit)
        {
            return;
        }

        if (_decidedBlock is not { } decidedBlock)
        {
            StartRound(round.Index + 1);
        }
        else
        {
            Step = ConsensusStep.EndCommit;
            _stepChangedSubject.OnNext((round, Step, decidedBlock.BlockHash));
            _completedSubject.OnNext((decidedBlock, GetBlockCommit()));
        }
    }

    private void Dispatcher_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        => _exceptionOccurredSubject.OnNext((Exception)e.ExceptionObject);

    private (Block, int)? GetProposal() => Proposal is { } p ? (p.Block, p.ValidRound) : null;
}
