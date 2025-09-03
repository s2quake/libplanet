using System.Reactive.Subjects;
using Libplanet.Net.Threading;
using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet.Net.Consensus;

public sealed partial class Consensus(ImmutableSortedSet<Validator> validators, int height, ConsensusOptions options)
    : ServiceBase
{
    private readonly Subject<Round> _roundChangedSubject = new();
    private readonly Subject<Exception> _exceptionOccurredSubject = new();
    private readonly Subject<ConsensusStep> _timeoutOccurredSubject = new();
    private readonly Subject<(ConsensusStep, BlockHash BlockHash)> _stepChangedSubject = new();
    private readonly Subject<(Proposal, BlockHash)> _proposalClaimedSubject = new();
    private readonly Subject<Block> _preVoteMaj23ObservedSubject = new();
    private readonly Subject<Block> _preCommitMaj23ObservedSubject = new();
    private readonly Subject<(Block, BlockCommit)> _finalizedSubject = new();

    private readonly Subject<Proposal> _proposedSubject = new();
    private readonly Subject<Vote> _preVotedSubject = new();
    private readonly Subject<Vote> _preCommittedSubject = new();

    private readonly Dictionary<BlockHash, bool> _blockValidationCache = [];
    private readonly RoundCollection _rounds = new(height, validators);
    private readonly ILogger<Consensus> _logger = options.Logger;

    private Dispatcher? _dispatcher;
    private Proposal? _preVoteProposal;
    private Proposal? _decidedProposal;
    private Round? _round;

    public Consensus(ImmutableSortedSet<Validator> validators, int height)
        : this(validators, height, new ConsensusOptions())
    {
    }

    public Consensus(ImmutableSortedSet<Validator> validators)
        : this(validators, height: 1, new ConsensusOptions())
    {
    }

    public Consensus(ImmutableSortedSet<Validator> validators, ConsensusOptions options)
        : this(validators, height: 1, options)
    {
    }

    public IObservable<Round> RoundChanged => _roundChangedSubject;

    public IObservable<Exception> ExceptionOccurred => _exceptionOccurredSubject;

    public IObservable<ConsensusStep> TimeoutOccurred => _timeoutOccurredSubject;

    public IObservable<(ConsensusStep Step, BlockHash BlockHash)> StepChanged => _stepChangedSubject;

    public IObservable<(Proposal Proposal, BlockHash BlockHash)> ProposalClaimed => _proposalClaimedSubject;

    public IObservable<Block> PreVoteMaj23Observed => _preVoteMaj23ObservedSubject;

    public IObservable<Block> PreCommitMaj23Observed => _preCommitMaj23ObservedSubject;

    public IObservable<(Block Block, BlockCommit BlockCommit)> Finalized => _finalizedSubject;

    public IObservable<Proposal> Proposed => _proposedSubject;

    public IObservable<Vote> PreVoted => _preVotedSubject;

    public IObservable<Vote> PreCommitted => _preCommittedSubject;

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

    public RoundCollection Rounds => _rounds;

    public ConsensusStep Step { get; private set; }

    public Proposal? Proposal { get; private set; }

    public Proposal? ValidProposal { get; private set; }

    public ImmutableSortedSet<Validator> Validators => validators;

    public bool AddPreVoteMaj23(Maj23 maj23)
    {
        var round = _rounds[maj23.Round];
        var maj23s = round.PreVoteMaj23s;
        if (!maj23s.Contains(maj23.Validator))
        {
            maj23s.Add(maj23);
            return true;
        }

        return false;
    }

    public bool AddPreCommitMaj23(Maj23 maj23)
    {
        var round = _rounds[maj23.Round];
        var maj23s = round.PreCommitMaj23s;
        if (!maj23s.Contains(maj23.Validator))
        {
            maj23s.Add(maj23);
            return true;
        }

        return false;
    }

    public async Task ProposeAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        await _dispatcher.InvokeAsync(_ =>
        {
            SetProposal(proposal);
            LogProposed(_logger, proposal.Height, proposal.Round, proposal.Validator);
            ProcessGenericUponRules();
        }, cancellationToken);
    }

    public async Task PreVoteAsync(Vote vote, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        if (vote.Height != Height)
        {
            throw new ArgumentException(
                $"Height of vote {vote.Height} does not match expected height {Height}.", nameof(vote));
        }

        if (vote.Type is not VoteType.PreVote)
        {
            throw new ArgumentException("Type of vote must be PreVote.", nameof(vote));
        }

        if (vote.Round < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vote), "Round of vote must be non-negative.");
        }

        await _dispatcher.PostAsync(() =>
        {
            var round = _rounds[vote.Round];
            round.PreVote(vote);
            _preVotedSubject.OnNext(vote);
            LogPreVoted(_logger, vote.Height, vote.Round, vote.Validator);
            ProcessHeightOrRoundUponRules(vote);
            ProcessGenericUponRules();
        }, cancellationToken);
    }

    public async Task PreCommitAsync(Vote vote, CancellationToken cancellationToken)
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

        await _dispatcher.PostAsync(() =>
        {
            var round = _rounds[vote.Round];
            round.PreCommit(vote);
            _preCommittedSubject.OnNext(vote);
            LogPreCommitted(_logger, vote.Height, vote.Round, vote.Validator);
            ProcessHeightOrRoundUponRules(vote);
            ProcessGenericUponRules();
        }, cancellationToken);
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

        Proposal = null;
        ValidProposal = null;
        _preVoteProposal = null;
        _decidedProposal = null;
        _round = null;
        Step = ConsensusStep.Default;
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
        _timeoutOccurredSubject.Dispose();
        _stepChangedSubject.Dispose();
        _proposalClaimedSubject.Dispose();
        _preVoteMaj23ObservedSubject.Dispose();
        _preCommitMaj23ObservedSubject.Dispose();
        _finalizedSubject.Dispose();

        _proposedSubject.Dispose();
        _preVotedSubject.Dispose();
        _preCommittedSubject.Dispose();

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
                _timeoutOccurredSubject.OnNext(ConsensusStep.Propose);
                LogTimeoutOccurred(_logger, Height, round.Index, ConsensusStep.Propose);
                EnterPreVoteStep(round, default);
                ProcessGenericUponRules();
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
                _timeoutOccurredSubject.OnNext(ConsensusStep.PreVote);
                LogTimeoutOccurred(_logger, Height, round.Index, ConsensusStep.PreVote);
                EnterPreCommitStep(round, default);
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
                _timeoutOccurredSubject.OnNext(ConsensusStep.PreCommit);
                LogTimeoutOccurred(_logger, Height, round.Index, ConsensusStep.PreCommit);
                EnterEndCommitStep(round);
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
        _stepChangedSubject.OnNext((Step, default));
        LogRoundStarted(_logger, Height, Round.Index);
        PostProposeTimeout(Round);
    }

    private void SetProposal(Proposal proposal)
    {
        var round = Round;
        var roundIndex = Round.Index;

        if (Validators.GetProposer(Height, roundIndex).Address != proposal.Validator)
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
        if (round.PreVotes.TryGetMajority23(out var blockHash1) && blockHash1 != proposal.BlockHash)
        {
            var message = $"Given proposal's block hash {proposal.BlockHash} does not match " +
                          $"with the collected +2/3 preVotes' block hash {blockHash1}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        if (round.PreCommits.TryGetMajority23(out var blockHash2) && blockHash2 != proposal.BlockHash)
        {
            var message = $"Given proposal's block hash {proposal.BlockHash} does not match " +
                          $"with the collected +2/3 preCommits' block hash {blockHash2}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        if (Proposal is not null)
        {
            throw new InvalidOperationException($"Proposal already exists for height {Height} and round {Round}");
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

        var proposal = Proposal;
        if (Step == ConsensusStep.Propose
            && proposal is not null
            && proposal.ValidRound == -1)
        {
            var lockedRound = _preVoteProposal?.ValidRound ?? -1;
            var lockedBlock = _preVoteProposal?.Block;
            if (IsValid(proposal.Block) && (lockedRound == -1 || lockedBlock == proposal.Block))
            {
                EnterPreVoteStep(Round, proposal.BlockHash);
            }
            else
            {
                EnterPreVoteStep(Round, default);
            }
        }

        if (Step == ConsensusStep.Propose
            && proposal is not null
            && proposal.ValidRound >= 0
            && proposal.ValidRound < roundIndex
            && _rounds[proposal.ValidRound].PreVotes.TryGetMajority23(out var blockHash2)
            && blockHash2 == proposal.BlockHash)
        {
            var lockedRound = _preVoteProposal?.ValidRound ?? -1;
            var lockedBlock = _preVoteProposal?.Block;
            if (IsValid(proposal.Block) && (lockedRound <= proposal.ValidRound || lockedBlock == proposal.Block))
            {
                EnterPreVoteStep(Round, proposal.BlockHash);
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
            && proposal is not null
            && round.PreVotes.TryGetMajority23(out var blockHash3)
            && blockHash3 == proposal.BlockHash
            && IsValid(proposal.Block)
            && !round.HasTwoThirdsPreVoteTypes)
        {
            round.HasTwoThirdsPreVoteTypes = true;
            if (Step == ConsensusStep.PreVote)
            {
                _preVoteProposal = proposal;
                EnterPreCommitWait(round, proposal.BlockHash);

                _preVoteMaj23ObservedSubject.OnNext(proposal.Block);
            }

            ValidProposal = proposal;
        }

        if (Step == ConsensusStep.PreVote && round.PreVotes.TryGetMajority23(out var blockHash4))
        {
            if (blockHash4 == default)
            {
                EnterPreCommitWait(round, default);
            }
            else if (proposal is not null && proposal.BlockHash != blockHash4)
            {
                Proposal = null;
                _proposalClaimedSubject.OnNext((proposal, blockHash4));
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
        var preCommits = round.PreCommits;
        if (Proposal is { } proposal
            && preCommits.TryGetMajority23(out var blockHash)
            && blockHash == proposal.BlockHash
            && IsValid(proposal.Block))
        {
            _decidedProposal = proposal;
            _preCommitMaj23ObservedSubject.OnNext(_decidedProposal.Block);
            EnterEndCommitWait(Round);
            return;
        }

        var preVotes = round.PreVotes;
        if (roundIndex > Round.Index && preVotes.HasOneThirdsAny)
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
        _stepChangedSubject.OnNext((Step, blockHash));
        LogStepChanged(_logger, Height, round.Index, Step, blockHash);
    }

    private void EnterPreCommitStep(Round round, BlockHash blockHash)
    {
        if (_round != round || Step >= ConsensusStep.PreCommit)
        {
            return;
        }

        Step = ConsensusStep.PreCommit;
        _stepChangedSubject.OnNext((Step, blockHash));
        LogStepChanged(_logger, Height, round.Index, Step, blockHash);
    }

    private void EnterEndCommitStep(Round round)
    {
        if (round != _round || Step is ConsensusStep.Default or ConsensusStep.EndCommit)
        {
            return;
        }

        if (_decidedProposal is not { } decidedProposal)
        {
            StartRound(round.Index + 1);
        }
        else
        {
            Step = ConsensusStep.EndCommit;
            _stepChangedSubject.OnNext((Step, decidedProposal.BlockHash));
            _finalizedSubject.OnNext((decidedProposal.Block, round.PreCommits.GetBlockCommit()));
            LogStepChanged(_logger, Height, round.Index, Step, decidedProposal.BlockHash);
        }
    }

    private void Dispatcher_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        => _exceptionOccurredSubject.OnNext((Exception)e.ExceptionObject);
}
