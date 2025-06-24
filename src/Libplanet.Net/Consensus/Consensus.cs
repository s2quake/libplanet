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

public partial class Consensus : IAsyncDisposable
{
    private readonly ConsensusOptions _options;

    private readonly Subject<int> _startedSubject = new();
    private readonly Subject<int> _roundStartedSubject = new();
    private readonly Subject<ConsensusMessage> _messagePublishedSubject = new();
    private readonly Subject<Exception> _exceptionOccurredSubject = new();
    private readonly Subject<ConsensusState> _stateChangedSubject = new();
    private readonly Subject<ConsensusStep> _stepChangedSubject = new();

    private readonly Blockchain _blockchain;
    private readonly ImmutableSortedSet<Validator> _validators;
    private readonly Channel<ConsensusMessage> _messageRequests;
    private readonly Dispatcher _dispatcher = new();
    private readonly HeightContext _heightContext;
    private readonly ISigner _signer;
    private readonly HashSet<int> _hasTwoThirdsPreVoteTypes = [];
    private readonly HashSet<int> _preVoteTimeoutFlags = [];
    private readonly HashSet<int> _preCommitTimeoutFlags = [];
    private readonly HashSet<int> _preCommitWaitFlags = [];
    private readonly HashSet<int> _endCommitWaitFlags = [];
    private readonly EvidenceExceptionCollector _evidenceCollector = new();
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ICache<BlockHash, bool> _blockValidationCache;
    private Block? _lockedBlock;
    private int _lockedRound = -1;
    private Block? _validBlock;
    private int _validRound = -1;
    private Block? _decidedBlock;
    private bool _disposed;
    private ConsensusStep _step;

    public Consensus(Blockchain blockchain, int height, ISigner signer, ConsensusOptions options)
    {
        if (height < 1)
        {
            throw new ArgumentException($"Given {nameof(height)} must be positive: {height}", nameof(height));
        }

        _signer = signer;
        Height = height;
        _blockchain = blockchain;
        _messageRequests = Channel.CreateUnbounded<ConsensusMessage>();
        _validators = blockchain.GetValidators(height);
        _heightContext = new HeightContext(height, _validators);
        _cancellationTokenSource = new CancellationTokenSource();
        _blockValidationCache = new ConcurrentLruBuilder<BlockHash, bool>()
            .WithCapacity(128)
            .Build();

        _options = options;
    }

    public IObservable<int> Started => _startedSubject;

    public IObservable<int> RoundStarted => _roundStartedSubject;

    public IObservable<ConsensusMessage> MessagePublished => _messagePublishedSubject;

    public IObservable<Exception> ExceptionOccurred => _exceptionOccurredSubject;

    public IObservable<ConsensusState> StateChanged => _stateChangedSubject;

    public IObservable<ConsensusStep> StepChanged => _stepChangedSubject;

    public int Height { get; }

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
            Validator = _signer.Address,
            VoteType = voteType,
            VoteBits = [.. voteBits],
        }.Sign(_signer);
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
            Validator = _signer.Address,
            ValidatorPower = _validators.GetValidator(_signer.Address).Power,
            Type = voteType,
        }.Sign(_signer);
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
            Validator = _signer.Address,
            VoteType = voteType,
        }.Sign(_signer);
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
        _ = Task.Run(() => _blockchain.Append(block, GetBlockCommit()));
    }

    private async Task EnterPreCommitWait(int round, BlockHash blockHash, CancellationToken cancellationToken)
    {
        if (!_preCommitWaitFlags.Add(round))
        {
            return;
        }

        await Task.Delay(_options.EnterPreCommitDelay, cancellationToken);
        await _dispatcher.InvokeAsync(_ =>
        {
            EnterPreCommit(round, blockHash);
            ProcessGenericUponRules();
        }, cancellationToken);
    }

    private async Task EnterEndCommitWait(int round, CancellationToken cancellationToken)
    {
        if (!_endCommitWaitFlags.Add(round))
        {
            return;
        }

        await Task.Delay(_options.EnterEndCommitDelay, cancellationToken);
        await _dispatcher.InvokeAsync(_ =>
        {
            EnterEndCommit(round);
            ProcessGenericUponRules();
        }, cancellationToken);
    }

    private async Task PostProposeTimeoutAsync(int round, CancellationToken cancellationToken)
    {
        var timeout = _options.TimeoutPropose(round);
        await Task.Delay(timeout, cancellationToken);
        await _dispatcher.InvokeAsync(_ =>
        {
            if (round == Round && Step == ConsensusStep.Propose)
            {
                EnterPreVote(round, default);
                ProcessGenericUponRules();
            }
        }, cancellationToken);
    }

    private async Task PostPreVoteTimeoutAsync(int round, CancellationToken cancellationToken)
    {
        if (_preCommitTimeoutFlags.Contains(round) || !_preVoteTimeoutFlags.Add(round))
        {
            return;
        }

        var timeout = _options.TimeoutPreVote(round);
        await Task.Delay(timeout, cancellationToken);
        await _dispatcher.InvokeAsync(_ =>
        {
            if (round == Round && Step == ConsensusStep.PreVote)
            {
                EnterPreCommit(round, default);
                ProcessGenericUponRules();
            }
        }, cancellationToken);
    }

    private async Task PostPreCommitTimeoutAsync(int round, CancellationToken cancellationToken)
    {
        if (!_preCommitTimeoutFlags.Add(round))
        {
            return;
        }

        var timeout = _options.TimeoutPreCommit(round);
        await Task.Delay(timeout, cancellationToken);
        await _dispatcher.InvokeAsync(_ =>
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
        }, cancellationToken);
    }

    private void StartRound(int round)
    {
        Round = round;
        _heightContext.Round = round;
        Proposal = null;
        Step = ConsensusStep.Propose;
        if (_validators.GetProposer(Height, Round).Address == _signer.Address
            && (_validBlock ?? GetValue()) is Block proposalBlock)
        {
            var proposal = new ProposalMetadata
            {
                Height = Height,
                Round = Round,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _signer.Address,
                ValidRound = _validRound,
            }.Sign(_signer, proposalBlock);

            _messagePublishedSubject.OnNext(new ConsensusProposalMessage { Proposal = proposal });
        }
        else
        {
            _ = PostProposeTimeoutAsync(Round, _cancellationTokenSource.Token);
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
            _ = PostPreVoteTimeoutAsync(Round, _cancellationTokenSource.Token);
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
                _ = EnterPreCommitWait(Round, p3.Block.BlockHash, default);

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
                _ = EnterPreCommitWait(Round, default, default);
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
                            Validator = _signer.Address,
                        }.Sign(_signer),
                    });
            }
        }

        if (_heightContext.PreCommits(Round).HasTwoThirdsAny)
        {
            _ = PostPreCommitTimeoutAsync(Round, _cancellationTokenSource.Token);
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
            _ = EnterEndCommitWait(Round, default);
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
}
