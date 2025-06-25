using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Extensions;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public partial class Consensus(Blockchain blockchain, int height, ISigner signer, ConsensusOptions options)
    : IAsyncDisposable
{
    private readonly Subject<int> _startedSubject = new();
    private readonly Subject<int> _roundStartedSubject = new();
    private readonly Subject<ConsensusMessage> _messagePublishedSubject = new();
    private readonly Subject<Exception> _exceptionOccurredSubject = new();
    private readonly Subject<ConsensusState> _stateChangedSubject = new();
    private readonly Subject<ConsensusStep> _stepChangedSubject = new();

    private readonly ImmutableSortedSet<Validator> _validators = blockchain.GetValidators(height);
    private readonly Channel<ConsensusMessage> _messageRequests = Channel.CreateUnbounded<ConsensusMessage>();
    private readonly Dispatcher _dispatcher = new();
    private readonly HeightContext _heightContext = new(height, blockchain.GetValidators(height));
    private readonly HashSet<int> _hasTwoThirdsPreVoteTypes = [];
    private readonly HashSet<int> _preVoteTimeoutFlags = [];
    private readonly HashSet<int> _preCommitTimeoutFlags = [];
    private readonly HashSet<int> _preCommitWaitFlags = [];
    private readonly HashSet<int> _endCommitWaitFlags = [];
    private readonly EvidenceExceptionCollector _evidenceCollector = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ICache<BlockHash, bool> _blockValidationCache = new ConcurrentLruBuilder<BlockHash, bool>()
        .WithCapacity(128)
        .Build();

    private Block? _lockedBlock;
    private int _lockedRound = -1;
    private Block? _validBlock;
    private int _validRound = -1;
    private Block? _decidedBlock;
    private bool _disposed;
    private ConsensusStep _step;

    public IObservable<int> Started => _startedSubject;

    public IObservable<int> RoundStarted => _roundStartedSubject;

    public IObservable<ConsensusMessage> MessagePublished => _messagePublishedSubject;

    public IObservable<Exception> ExceptionOccurred => _exceptionOccurredSubject;

    public IObservable<ConsensusState> StateChanged => _stateChangedSubject;

    public IObservable<ConsensusStep> StepChanged => _stepChangedSubject;

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

    public Proposal? Proposal { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _cancellationTokenSource.CancelAsync();
            _messageRequests.Writer.TryComplete();
            await _dispatcher.DisposeAsync();
            _cancellationTokenSource.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public BlockCommit GetBlockCommit()
    {
        try
        {
            return _heightContext.PreCommits(Round).ToBlockCommit();
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
            VoteType.PreVote => _heightContext.PreVotes(round).BitArrayByBlockHash(blockHash),
            VoteType.PreCommit => _heightContext.PreCommits(round).BitArrayByBlockHash(blockHash),
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
        try
        {
            if (_heightContext.SetPeerMaj23(maj23))
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
            votes = voteSetBits.VoteType switch
            {
                VoteType.PreVote =>
                _heightContext.PreVotes(voteSetBits.Round).MappedList().Where(
                    (vote, index)
                    => !voteSetBits.VoteBits[index]
                    && vote is { }
                    && vote.Type == VoteType.PreVote).Select(vote => vote!),
                VoteType.PreCommit =>
                _heightContext.PreCommits(voteSetBits.Round).MappedList().Where(
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

    public EvidenceException[] CollectEvidenceExceptions() => _evidenceCollector.Flush();

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

    private Vote CreateVote(int round, BlockHash blockHash, VoteType voteType)
    {
        if (voteType is VoteType.Null or VoteType.Unknown)
        {
            var message = $"{nameof(voteType)} must be either {VoteType.PreVote} or {VoteType.PreCommit}" +
                          $"to create a valid signed vote.";
            throw new ArgumentException(message, nameof(voteType));
        }

        return new VoteMetadata
        {
            Height = Height,
            Round = round,
            BlockHash = blockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            ValidatorPower = _validators.GetValidator(signer.Address).Power,
            Type = voteType,
        }.Sign(signer);
    }

    private Maj23 CreateMaj23(int round, BlockHash blockHash, VoteType voteType)
    {
        if (voteType is VoteType.Null or VoteType.Unknown)
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
            Validator = signer.Address,
            VoteType = voteType,
        }.Sign(signer);
    }

    private (Block, int)? GetProposal() => Proposal is { } p ? (p.Block, p.ValidRound) : null;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Step != ConsensusStep.Default)
        {
            throw new InvalidOperationException(
                $"Context cannot be started unless its state is {ConsensusStep.Default} " +
                $"but its current step is {Step}");
        }

        _startedSubject.OnNext(Height);
        await _dispatcher.InvokeAsync(_ =>
        {
            StartRound(0);
            ProcessGenericUponRules();
        }, _cancellationTokenSource.Token);
        _ = MessageConsumerTask(_cancellationTokenSource.Token);
    }

    private async Task MessageConsumerTask(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = await _messageRequests.Reader.ReadAsync(cancellationToken);
                await _dispatcher.InvokeAsync(_ =>
                {
                    if (HandleMessage(message))
                    {
                        ProcessHeightOrRoundUponRules(message);
                    }

                    ProcessGenericUponRules();
                }, cancellationToken);
            }
            catch (Exception e)
            {
                _exceptionOccurredSubject.OnNext(e);
                throw;
            }
        }
    }

    internal void ProduceMessage(ConsensusMessage message)
    {
        _ = _messageRequests.Writer.WriteAsync(message);
    }

    private void ConsumeMutation()
    {
        // System.Action mutation = await _mutationRequests.Reader.ReadAsync(cancellationToken);
        // var prevState = new ContextState
        // {
        //     VoteCount = _heightVoteSet.Count,
        //     Height = Height,
        //     Round = Round,
        //     Step = Step,
        //     Proposal = Proposal?.BlockHash,
        // };
        // mutation();
        // var nextState = new ContextState
        // {
        //     VoteCount = _heightVoteSet.Count,
        //     Height = Height,
        //     Round = Round,
        //     Step = Step,
        //     Proposal = Proposal?.BlockHash,
        // };
        // while (!prevState.Equals(nextState))
        // {
        //     _stateChangedSubject.OnNext(nextState);
        //     prevState = new ContextState
        //     {
        //         VoteCount = _heightVoteSet.Count,
        //         Height = Height,
        //         Round = Round,
        //         Step = Step,
        //         Proposal = Proposal?.BlockHash,
        //     };
        //     ProcessGenericUponRules();
        //     nextState = new ContextState
        //     {
        //         VoteCount = _heightVoteSet.Count,
        //         Height = Height,
        //         Round = Round,
        //         Step = Step,
        //         Proposal = Proposal?.BlockHash,
        //     };
        // }

        // MutationConsumed?.Invoke(this, mutation);
    }

    private void AppendBlock(Block block)
    {
        _ = Task.Run(() => blockchain.Append(block, GetBlockCommit()));
    }

    private void EnterPreCommitWait(int round, BlockHash blockHash)
    {
        _dispatcher.VerifyAccess();
        if (!_preCommitWaitFlags.Add(round))
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
        _dispatcher.VerifyAccess();
        if (!_endCommitWaitFlags.Add(round))
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
        _dispatcher.VerifyAccess();
        if (_preCommitTimeoutFlags.Contains(round) || !_preVoteTimeoutFlags.Add(round))
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
        _dispatcher.VerifyAccess();
        if (!_preCommitTimeoutFlags.Add(round))
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
        _dispatcher.VerifyAccess();

        Round = round;
        _heightContext.Round = round;
        Proposal = null;
        Step = ConsensusStep.Propose;
        if (_validators.GetProposer(Height, Round).Address == signer.Address
            && (_validBlock ?? GetValue()) is Block proposalBlock)
        {
            var proposal = new ProposalMetadata
            {
                Height = Height,
                Round = Round,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = signer.Address,
                ValidRound = _validRound,
            }.Sign(signer, proposalBlock);

            _messagePublishedSubject.OnNext(new ConsensusProposalMessage { Proposal = proposal });
        }
        else
        {
            PostProposeTimeout(Round);
        }

        _roundStartedSubject.OnNext(round);
    }

    private bool HandleMessage(ConsensusMessage message)
    {
        try
        {
            if (message.Height != Height)
            {
                throw new InvalidOperationException(
                    $"Given message's height {message.Height} is invalid");
            }

            if (!_validators.Contains(message.Validator))
            {
                throw new InvalidOperationException(
                    $"Given message's validator {message.Validator} is invalid");
            }

            if (message is ConsensusProposalMessage proposalMessage)
            {
                SetProposal(proposalMessage.Proposal);
            }

            if (message is ConsensusVoteMessage voteMessage)
            {
                switch (voteMessage)
                {
                    case ConsensusPreVoteMessage preVote:
                        {
                            _heightContext.AddVote(preVote.PreVote);
                            break;
                        }

                    case ConsensusPreCommitMessage preCommit:
                        {
                            _heightContext.AddVote(preCommit.PreCommit);
                            break;
                        }
                }

                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            _exceptionOccurredSubject.OnNext(e);
            return false;
        }
    }

    private void SetProposal(Proposal proposal)
    {
        if (!_validators.GetProposer(Height, Round).Address.Equals(proposal.Validator))
        {
            throw new InvalidProposalException(
                $"Given proposal's proposer {proposal.Validator} is not the " +
                $"proposer for the current height {Height} and round {Round}",
                proposal);
        }

        if (proposal.Round != Round)
        {
            throw new InvalidProposalException(
                $"Given proposal's round {proposal.Round} does not match" +
                $" with the current round {Round}",
                proposal);
        }

        // Should check if +2/3 votes already collected and the proposal does not match
        if (_heightContext.PreVotes(Round).TwoThirdsMajority(out var preVoteMaj23) &&
            !proposal.BlockHash.Equals(preVoteMaj23))
        {
            throw new InvalidProposalException(
                $"Given proposal's block hash {proposal.BlockHash} does not match" +
                $" with the collected +2/3 preVotes' block hash {preVoteMaj23}",
                proposal);
        }

        if (_heightContext.PreCommits(Round).TwoThirdsMajority(out var preCommitMaj23) &&
            !proposal.BlockHash.Equals(preCommitMaj23))
        {
            throw new InvalidProposalException(
                $"Given proposal's block hash {proposal.BlockHash} does not match" +
                $" with the collected +2/3 preCommits' block hash {preCommitMaj23}",
                proposal);
        }

        if (Proposal is null)
        {
            Proposal = proposal;
        }
        else
        {
            throw new InvalidProposalException(
                $"Proposal already exists for height {Height} and round {Round}",
                proposal);
        }
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
            && _heightContext.PreVotes(p2.ValidRound).TwoThirdsMajority(out BlockHash hash1)
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

        if (Step == ConsensusStep.PreVote && _heightContext.PreVotes(Round).HasTwoThirdsAny)
        {
            PostPreVoteTimeout(Round);
        }

        if ((Step == ConsensusStep.PreVote || Step == ConsensusStep.PreCommit)
            && propose is { } p3
            && _heightContext.PreVotes(Round).TwoThirdsMajority(out BlockHash hash2)
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

                // Maybe need to broadcast periodically?
                _messagePublishedSubject.OnNext(
                    new ConsensusMaj23Message
                    {
                        Maj23 = CreateMaj23(Round, p3.Block.BlockHash, VoteType.PreVote),
                    });
            }

            _validBlock = p3.Block;
            _validRound = Round;
        }

        if (Step == ConsensusStep.PreVote
            && _heightContext.PreVotes(Round).TwoThirdsMajority(out BlockHash hash3))
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
                _messagePublishedSubject.OnNext(
                    new ConsensusProposalClaimMessage
                    {
                        ProposalClaim = new ProposalClaimMetadata
                        {
                            Height = Height,
                            Round = Round,
                            BlockHash = hash3,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = signer.Address,
                        }.Sign(signer),
                    });
            }
        }

        if (_heightContext.PreCommits(Round).HasTwoThirdsAny)
        {
            PostPreCommitTimeout(Round);
        }
    }

    private void ProcessHeightOrRoundUponRules(ConsensusMessage message)
    {
        if (Step == ConsensusStep.Default || Step == ConsensusStep.EndCommit)
        {
            return;
        }

        var round = message.Round;
        if ((message is ConsensusProposalMessage || message is ConsensusPreCommitMessage) &&
            GetProposal() is (Block block4, _) &&
            _heightContext.PreCommits(Round).TwoThirdsMajority(out BlockHash hash) &&
            block4.BlockHash.Equals(hash) &&
            IsValid(block4))
        {
            _decidedBlock = block4;

            // Maybe need to broadcast periodically?
            _messagePublishedSubject.OnNext(
                new ConsensusMaj23Message
                {
                    Maj23 = CreateMaj23(round, block4.BlockHash, VoteType.PreCommit),
                });
            EnterEndCommitWait(Round);
            return;
        }

        if (round > Round && _heightContext.PreVotes(round).HasOneThirdsAny)
        {
            StartRound(round);
        }
    }

    private void EnterPreVote(int round, BlockHash blockHash)
    {
        if (Round != round || Step >= ConsensusStep.PreVote)
        {
            // Round and step mismatch
            return;
        }

        Step = ConsensusStep.PreVote;
        _messagePublishedSubject.OnNext(
            new ConsensusPreVoteMessage { PreVote = CreateVote(round, blockHash, VoteType.PreVote) });
    }

    private void EnterPreCommit(int round, BlockHash blockHash)
    {
        if (Round != round || Step >= ConsensusStep.PreCommit)
        {
            // Round and step mismatch
            return;
        }

        Step = ConsensusStep.PreCommit;
        _messagePublishedSubject.OnNext(
            new ConsensusPreCommitMessage { PreCommit = CreateVote(round, blockHash, VoteType.PreCommit) });
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
            AppendBlock(block);
        }
        catch (Exception e)
        {
            _exceptionOccurredSubject.OnNext(e);
            return;
        }
    }

    private static int ValidateHeight(int height)
    {
        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
        }

        return height;
    }
}
