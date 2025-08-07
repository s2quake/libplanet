using System.Reactive;
using System.Reactive.Subjects;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class Consensus(
    Blockchain blockchain,
    int height,
    ISigner signer,
    ImmutableSortedSet<Validator> validators,
    ConsensusOptions options)
    : ServiceBase
{
    private readonly Subject<int> _roundChangedSubject = new();
    private readonly Subject<Exception> _exceptionOccurredSubject = new();
    private readonly Subject<(int Round, ConsensusStep Step)> _timeoutOccurredSubject = new();
    private readonly Subject<ConsensusStep> _stepChangedSubject = new();
    private readonly Subject<Proposal?> _proposalChangedSubject = new();
    private readonly Subject<(Block Block, BlockCommit BlockCommit)> _completedSubject = new();
    private readonly Subject<Vote> _voteAddedSubject = new();

    private readonly Subject<Vote> _shouldPreVoteSubject = new();
    private readonly Subject<Vote> _shouldPreCommitSubject = new();
    private readonly Subject<Maj23> _shouldQuorumReachSubject = new();
    private readonly Subject<ProposalClaim> _shouldProposalClaimSubject = new();
    private readonly Subject<Proposal> _shouldProposeSubject = new();

    // private readonly VoteContext _preVotes = new(height, VoteType.PreVote, validators);
    // private readonly VoteContext _preCommits = new(height, VoteType.PreCommit, validators);
    private readonly List<ConsensusRound> _roundList = [];
    private readonly HashSet<int> _hasTwoThirdsPreVoteTypes = [];
    private readonly HashSet<int> _preVoteTimeouts = [];
    private readonly HashSet<int> _preCommitTimeouts = [];
    private readonly HashSet<int> _preCommitWaits = [];
    private readonly HashSet<int> _endCommitWaits = [];
    private readonly ICache<BlockHash, bool> _blockValidationCache = new ConcurrentLruBuilder<BlockHash, bool>()
        .WithCapacity(128)
        .Build();

    private Dispatcher? _dispatcher;
    private Block? _lockedBlock;
    private int _lockedRound = -1;
    private Block? _validBlock;
    private int _validRound = -1;
    private Block? _decidedBlock;
    private ConsensusStep _step;
    private Proposal? _proposal;

    public Consensus(Blockchain blockchain, int height, ISigner signer, ConsensusOptions options)
        : this(blockchain, height, signer, blockchain.GetValidators(height), options)
    {
    }

    public IObservable<int> RoundChanged => _roundChangedSubject;

    public IObservable<Exception> ExceptionOccurred => _exceptionOccurredSubject;

    public IObservable<(int Round, ConsensusStep Step)> TimeoutOccurred => _timeoutOccurredSubject;

    public IObservable<ConsensusStep> StepChanged => _stepChangedSubject;

    public IObservable<Proposal?> ProposalChanged => _proposalChangedSubject;

    public IObservable<Vote> VoteAdded => _voteAddedSubject;

    public IObservable<(Block Block, BlockCommit BlockCommit)> Completed => _completedSubject;

    public IObservable<Vote> ShouldPreVote => _shouldPreVoteSubject;

    public IObservable<Vote> ShouldPreCommit => _shouldPreCommitSubject;

    public IObservable<Maj23> ShouldQuorumReach => _shouldQuorumReachSubject;

    public IObservable<ProposalClaim> ShouldProposalClaim => _shouldProposalClaimSubject;

    public IObservable<Proposal> ShouldPropose => _shouldProposeSubject;

    public Address Signer => signer.Address;

    public int Height { get; } = ValidateHeight(height);

    public int Round { get; private set; } = -1;

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

    public Proposal? Proposal
    {
        get => _proposal;
        private set
        {
            if (_proposal != value)
            {
                _proposal = value;
                _proposalChangedSubject.OnNext(value);
            }
        }
    }

    public BlockHash BlockHash { get; private set; }

    public ImmutableSortedSet<Validator> Validators { get; } = validators;

    public BlockCommit GetBlockCommit() => _roundList[Round].PreCommits.GetBlockCommit();

    public VoteSetBits GetVoteSetBits(int round, BlockHash blockHash, VoteType voteType)
    {
        // If executed in correct manner (called by Maj23),
        // _heightVoteSet.PreVotes(round) on below cannot throw KeyNotFoundException,
        // since RoundVoteSet has been already created on SetPeerMaj23.
        bool[] voteBits = voteType switch
        {
            VoteType.PreVote => _roundList[round].PreVotes.GetVoteBits(blockHash),
            VoteType.PreCommit => _roundList[round].PreCommits.GetVoteBits(blockHash),
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

    public bool AddMaj23(Maj23 maj23)
    {
        if (maj23.VoteType is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new ArgumentException("VoteType should be either PreVote or PreCommit.", nameof(maj23));
        }

        var round = _roundList[maj23.Round];

        var majorities = maj23.VoteType == VoteType.PreVote ? round.PreVoteMajorities : round.PreCommitMajorities;
        if (!majorities.ContainsKey(maj23.Validator))
        {
            majorities.Add(maj23);
            return true;
        }

        return false;
        // if ()
        // {
        //     var voteSetBits = GetVoteSetBits(maj23.Round, maj23.BlockHash, maj23.VoteType);
        //     return voteSetBits.VoteBits.All(b => b) ? null : voteSetBits;
        // }

        // return null;
    }

    public VoteSetBits GetVoteSetBits(Maj23 maj23)
    {
        if (maj23.Height != Height)
        {
            throw new ArgumentException(
                $"Maj23 height {maj23.Height} does not match expected height {Height}.", nameof(maj23));
        }

        if (maj23.Round < 0 || maj23.Round >= _roundList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(maj23.Round), "Round is out of range.");
        }

        var round = _roundList[maj23.Round];
        var votes = maj23.VoteType == VoteType.PreVote
            ? round.PreVotes
            : round.PreCommits;

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

        var round = _roundList[voteSetBits.Round];
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

        // foreach (var vote in voteList)
        // {
        //     yield return vote.Type switch
        //     {
        //         VoteType.PreVote => new ConsensusPreVoteMessage { PreVote = vote },
        //         VoteType.PreCommit => new ConsensusPreCommitMessage { PreCommit = vote },
        //         _ => throw new ArgumentException("VoteType should be PreVote or PreCommit.", nameof(vote)),
        //     };
        // }


        // IEnumerable<Vote> votes;
        // try
        // {
        //     votes = voteSetBits.VoteType switch
        //     {
        //         VoteType.PreVote =>
        //         _roundList[voteSetBits.Round].PreVotes.MappedList().Where(
        //             (vote, index)
        //             => !voteSetBits.VoteBits[index]
        //             && vote is { }
        //             && vote.Type == VoteType.PreVote).Select(vote => vote!),
        //         VoteType.PreCommit =>
        //         _roundList[voteSetBits.Round].PreCommits.MappedList().Where(
        //             (vote, index)
        //             => !voteSetBits.VoteBits[index]
        //             && vote is { }
        //             && vote.Type == VoteType.PreCommit).Select(vote => vote!),
        //         _ => throw new ArgumentException("VoteType should be PreVote or PreCommit.", nameof(voteSetBits)),
        //     };
        // }
        // catch (KeyNotFoundException)
        // {
        //     votes = [];
        // }

        // return votes.Select(GetMessage);

        // static ConsensusMessage GetMessage(Vote vote) => vote.Type switch
        // {
        //     VoteType.PreVote => new ConsensusPreVoteMessage { PreVote = vote },
        //     VoteType.PreCommit => new ConsensusPreCommitMessage { PreCommit = vote },
        //     _ => throw new ArgumentException("VoteType should be PreVote or PreCommit.", nameof(vote)),
        // };
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

    public void Post(Vote vote)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.Post(() =>
        {
            var voteContext = vote.Type is VoteType.PreVote ? _roundList[Round].PreVotes : _roundList[Round].PreCommits;
            voteContext.Add(vote);
            _voteAddedSubject.OnNext(vote);
            ProcessHeightOrRoundUponRules(vote);
            ProcessGenericUponRules();
        });
    }

    internal void Post(ConsensusMessage consensusMessage)
    {
        if (consensusMessage.Height != Height)
        {
            var message = $"ConsensusMessage height {consensusMessage.Height} does not match expected height {Height}.";
            throw new ArgumentException(message, nameof(consensusMessage));
        }

        if (consensusMessage is ConsensusPreVoteMessage preVoteMessage)
        {
            Post(preVoteMessage.PreVote);
        }
        else if (consensusMessage is ConsensusPreCommitMessage preCommitMessage)
        {
            Post(preCommitMessage.PreCommit);
        }
        else if (consensusMessage is ConsensusProposalMessage proposalMessage)
        {
            Propose(proposalMessage.Proposal);
        }
    }

    internal bool IsSigner(Address address)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        return Validators.GetProposer(Height, Round).Address == address;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _lockedBlock = null;
        _lockedRound = -1;
        _validBlock = null;
        _validRound = -1;
        _decidedBlock = null;
        Round = -1;
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
        _stepChangedSubject.Dispose();
        _proposalChangedSubject.Dispose();
        _completedSubject.Dispose();
        _voteAddedSubject.Dispose();
        _shouldPreVoteSubject.Dispose();
        _shouldPreCommitSubject.Dispose();
        _shouldQuorumReachSubject.Dispose();
        _shouldProposalClaimSubject.Dispose();
        _shouldProposeSubject.Dispose();
        await base.DisposeAsyncCore();
    }

    private static int ValidateHeight(int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return height;
    }

    private Block ProposeBlock() => blockchain.ProposeBlock(signer);

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
                blockchain.Validate(block);
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

    private void EnterPreCommitWait(int round, BlockHash blockHash)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        if (!_preCommitWaits.Add(round))
        {
            return;
        }

        var delay = options.EnterPreCommitDelay;
        _ = _dispatcher.PostAfterAsync(Invoke, delay, default);

        void Invoke(CancellationToken _)
        {
            EnterPreCommit(round, blockHash);
            ProcessGenericUponRules();
        }
    }

    private void EnterEndCommitWait(int round)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        if (!_endCommitWaits.Add(round))
        {
            return;
        }

        var delay = options.EnterEndCommitDelay;
        _ = _dispatcher.PostAfterAsync(Invoke, delay, default);

        void Invoke(CancellationToken _)
        {
            EnterEndCommit(round);
            ProcessGenericUponRules();
        }
    }

    private void PostProposeTimeout(int round)
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
            if (round == Round && Step == ConsensusStep.Propose)
            {
                _timeoutOccurredSubject.OnNext((round, ConsensusStep.Propose));
                if (Proposal is null)
                {
                    StartRound(round + 1);
                }
                else
                {
                    EnterPreVote(round, default);
                    ProcessGenericUponRules();
                }
            }
        }
    }

    private void PostPreVoteTimeout(int round)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        _dispatcher.VerifyAccess();
        if (_preCommitTimeouts.Contains(round) || !_preVoteTimeouts.Add(round))
        {
            return;
        }

        var timeout = options.TimeoutPreVote(round);
        _ = _dispatcher.PostAfterAsync(Invoke, timeout, default);

        void Invoke(CancellationToken _)
        {
            if (round == Round && Step == ConsensusStep.PreVote)
            {
                EnterPreCommit(round, default);
                _timeoutOccurredSubject.OnNext((round, ConsensusStep.PreVote));
                ProcessGenericUponRules();
            }
        }
    }

    private void PostPreCommitTimeout(int round)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("Consensus is not running.");
        }

        if (!_preCommitTimeouts.Add(round))
        {
            return;
        }

        var timeout = options.TimeoutPreCommit(round);
        _ = _dispatcher.PostAfterAsync(Invoke, timeout, StoppingToken);

        void Invoke(CancellationToken _)
        {
            if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
            {
                return;
            }

            if (round == Round)
            {
                EnterEndCommit(round);
                _timeoutOccurredSubject.OnNext((round, ConsensusStep.PreCommit));
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

        var consensusRound = new ConsensusRound(round, this);

        _roundList.Add(consensusRound);

        Round = round;
        Proposal = null;
        Step = ConsensusStep.Propose;
        _roundChangedSubject.OnNext(round);
        if (Validators.GetProposer(Height, Round).Address == signer.Address
            && (_validBlock ?? ProposeBlock()) is Block proposalBlock)
        {
            var proposal = new ProposalBuilder
            {
                Block = proposalBlock,
                Round = Round,
                Timestamp = DateTimeOffset.UtcNow,
                ValidRound = _validRound,
            }.Create(signer);
            _shouldProposeSubject.OnNext(proposal);
        }

        PostProposeTimeout(Round);
    }

    private void SetProposal(Proposal proposal)
    {
        if (Proposal is not null)
        {
            throw new InvalidOperationException($"Proposal already exists for height {Height} and round {Round}");
        }

        if (!Validators.GetProposer(Height, Round).Address.Equals(proposal.Validator))
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
        if (_roundList[Round].PreVotes.BlockHash != proposal.BlockHash)
        {
            var message = $"Given proposal's block hash {proposal.BlockHash} does not match " +
                          $"with the collected +2/3 preVotes' block hash {_roundList[Round].PreVotes.BlockHash}.";
            throw new ArgumentException(message, nameof(proposal));
        }

        if (_roundList[Round].PreCommits.BlockHash != proposal.BlockHash)
        {
            var message = $"Given proposal's block hash {proposal.BlockHash} does not match " +
                          $"with the collected +2/3 preCommits' block hash {_roundList[Round].PreCommits.BlockHash}.";
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
            && _roundList[p2.ValidRound].PreVotes.BlockHash == p2.Block.BlockHash)
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

        if (Step == ConsensusStep.PreVote && _roundList[Round].PreVotes.HasTwoThirdsAny)
        {
            PostPreVoteTimeout(Round);
        }

        if ((Step == ConsensusStep.PreVote || Step == ConsensusStep.PreCommit)
            && propose is { } p3
            && _roundList[Round].PreVotes.BlockHash == p3.Block.BlockHash
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
                _shouldQuorumReachSubject.OnNext(maj23);
            }

            _validBlock = p3.Block;
            _validRound = Round;
        }

        if (Step == ConsensusStep.PreVote)
        {
            var hash3 = _roundList[Round].PreVotes.BlockHash;
            if (hash3 == default)
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
                _shouldProposalClaimSubject.OnNext(proposalClaim);
            }
        }

        if (_roundList[Round].PreCommits.HasTwoThirdsAny)
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
        if (GetProposal() is (Block block4, _)
            && _roundList[Round].PreCommits.BlockHash == block4.BlockHash
            && IsValid(block4))
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
            _shouldQuorumReachSubject.OnNext(maj23);
            EnterEndCommitWait(Round);
            return;
        }

        if (round > Round && _roundList[round].PreVotes.HasOneThirdsAny)
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
            ValidatorPower = Validators.GetValidator(signer.Address).Power,
            Type = VoteType.PreVote,
        }.Sign(signer);
        Step = ConsensusStep.PreVote;
        _shouldPreVoteSubject.OnNext(vote);
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
            ValidatorPower = Validators.GetValidator(signer.Address).Power,
            Type = VoteType.PreCommit,
        }.Sign(signer);
        Step = ConsensusStep.PreCommit;
        _shouldPreCommitSubject.OnNext(vote);
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
        }
        else
        {
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
    }

    private void Dispatcher_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        => _exceptionOccurredSubject.OnNext((Exception)e.ExceptionObject);

    private (Block, int)? GetProposal() => Proposal is { } p ? (p.Block, p.ValidRound) : null;
}
