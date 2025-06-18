using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public partial class Context
{
    public void Start()
    {
        if (Step != ConsensusStep.Default)
        {
            throw new InvalidOperationException(
                $"Context cannot be started unless its state is {ConsensusStep.Default} " +
                $"but its current step is {Step}");
        }

        HeightStarted?.Invoke(this, Height);
        ProduceMutation(() => StartRound(0));

        // FIXME: Exceptions inside tasks should be handled properly.
        _ = MessageConsumerTask(_cancellationTokenSource.Token);
        _ = MutationConsumerTask(_cancellationTokenSource.Token);
    }

    internal async Task MessageConsumerTask(CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await ConsumeMessage(cancellationToken);
            }
            catch (OperationCanceledException oce)
            {
                ExceptionOccurred?.Invoke(this, oce);
                throw;
            }
            catch (Exception e)
            {
                ExceptionOccurred?.Invoke(this, e);
                throw;
            }
        }
    }

    internal async Task MutationConsumerTask(CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await ConsumeMutation(cancellationToken);
            }
            catch (OperationCanceledException oce)
            {
                ExceptionOccurred?.Invoke(this, oce);
                throw;
            }
            catch (EvidenceException e)
            {
                _evidenceCollector.Add(e);
            }
            catch (Exception e)
            {
                ExceptionOccurred?.Invoke(this, e);
                throw;
            }
        }
    }

    internal void ProduceMessage(ConsensusMessage message)
    {
        _messageRequests.Writer.WriteAsync(message);
    }

    private void ProduceMutation(System.Action mutation)
    {
        _mutationRequests.Writer.WriteAsync(mutation);
    }

    private async Task ConsumeMessage(CancellationToken cancellationToken)
    {
        ConsensusMessage message = await _messageRequests.Reader.ReadAsync(cancellationToken);
        ProduceMutation(() =>
        {
            if (AddMessage(message))
            {
                ProcessHeightOrRoundUponRules(message);
            }
        });

        MessageConsumed?.Invoke(this, message);
    }

    private async Task ConsumeMutation(CancellationToken cancellationToken)
    {
        System.Action mutation = await _mutationRequests.Reader.ReadAsync(cancellationToken);
        var prevState = new ContextState
        {
            VoteCount = _heightVoteSet.Count,
            Height = Height,
            Round = Round,
            Step = Step,
            Proposal = Proposal?.BlockHash,
        };
        mutation();
        var nextState = new ContextState
        {
            VoteCount = _heightVoteSet.Count,
            Height = Height,
            Round = Round,
            Step = Step,
            Proposal = Proposal?.BlockHash,
        };
        while (!prevState.Equals(nextState))
        {
            StateChanged?.Invoke(this, nextState);
            prevState = new ContextState
            {
                VoteCount = _heightVoteSet.Count,
                Height = Height,
                Round = Round,
                Step = Step,
                Proposal = Proposal?.BlockHash,
            };
            ProcessGenericUponRules();
            nextState = new ContextState
            {
                VoteCount = _heightVoteSet.Count,
                Height = Height,
                Round = Round,
                Step = Step,
                Proposal = Proposal?.BlockHash,
            };
        }

        MutationConsumed?.Invoke(this, mutation);
    }

    private void AppendBlock(Block block)
    {
        _ = Task.Run(() => _blockchain.Append(block, GetBlockCommit()));
    }

    private async Task EnterPreCommitWait(int round, BlockHash hash)
    {
        if (!_preCommitWaitFlags.Add(round))
        {
            return;
        }

        if (_contextOption.EnterPreCommitDelay > 0)
        {
            await Task.Delay(
                _contextOption.EnterPreCommitDelay,
                _cancellationTokenSource.Token);
        }

        ProduceMutation(() => EnterPreCommit(round, hash));
    }

    private async Task EnterEndCommitWait(int round)
    {
        if (!_endCommitWaitFlags.Add(round))
        {
            return;
        }

        if (_contextOption.EnterEndCommitDelay > 0)
        {
            await Task.Delay(
                _contextOption.EnterEndCommitDelay,
                _cancellationTokenSource.Token);
        }

        ProduceMutation(() => EnterEndCommit(round));
    }

    private async Task OnTimeoutPropose(int round)
    {
        TimeSpan timeout = TimeoutPropose(round);
        await Task.Delay(timeout, _cancellationTokenSource.Token);
        ProduceMutation(() => ProcessTimeoutPropose(round));
    }

    private async Task OnTimeoutPreVote(int round)
    {
        if (_preCommitTimeoutFlags.Contains(round) || !_preVoteTimeoutFlags.Add(round))
        {
            return;
        }

        TimeSpan timeout = TimeoutPreVote(round);
        await Task.Delay(timeout, _cancellationTokenSource.Token);
        ProduceMutation(() => ProcessTimeoutPreVote(round));
    }

    private async Task OnTimeoutPreCommit(int round)
    {
        if (!_preCommitTimeoutFlags.Add(round))
        {
            return;
        }

        TimeSpan timeout = TimeoutPreCommit(round);
        await Task.Delay(timeout, _cancellationTokenSource.Token);
        ProduceMutation(() => ProcessTimeoutPreCommit(round));
    }
}
