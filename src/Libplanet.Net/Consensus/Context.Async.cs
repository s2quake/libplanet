using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public partial class Context
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Step != ConsensusStep.Default)
        {
            throw new InvalidOperationException(
                $"Context cannot be started unless its state is {ConsensusStep.Default} " +
                $"but its current step is {Step}");
        }

        _heightStartedSubject.OnNext(Height);
        await _dispatcher.InvokeAsync(_ => StartRound(0), _cancellationTokenSource.Token);

        // FIXME: Exceptions inside tasks should be handled properly.
        _ = MessageConsumerTask(_cancellationTokenSource.Token);
        // _ = MutationConsumerTask(_cancellationTokenSource.Token);
    }

    internal async Task MessageConsumerTask(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeMessage(cancellationToken);
            }
            catch (Exception e)
            {
                _exceptionOccurredSubject.OnNext(e);
                throw;
            }
        }
    }

    // internal async Task MutationConsumerTask(CancellationToken cancellationToken)
    // {
    //     while (true)
    //     {
    //         try
    //         {
    //             await ConsumeMutation(cancellationToken);
    //         }
    //         catch (OperationCanceledException oce)
    //         {
    //             _exceptionOccurredSubject.OnNext(oce);
    //             throw;
    //         }
    //         catch (EvidenceException e)
    //         {
    //             _evidenceCollector.Add(e);
    //         }
    //         catch (Exception e)
    //         {
    //             _exceptionOccurredSubject.OnNext(e);
    //             throw;
    //         }
    //     }
    // }

    internal void ProduceMessage(ConsensusMessage message)
    {
        _messageRequests.Writer.WriteAsync(message);
    }

    // private ValueTask ProduceMutationAsync(Action mutation, CancellationToken cancellationToken)
    // {
    //     return _mutationRequests.Writer.WriteAsync(mutation, cancellationToken);
    // }

    private async Task ConsumeMessage(CancellationToken cancellationToken)
    {
        var message = await _messageRequests.Reader.ReadAsync(cancellationToken);
        await _dispatcher.InvokeAsync(_ =>
        {
            if (AddMessage(message))
            {
                ProcessHeightOrRoundUponRules(message);
            }
        }, cancellationToken);

        MessageConsumed?.Invoke(this, message);
    }

    private async Task ConsumeMutation(CancellationToken cancellationToken)
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

    private async Task EnterPreCommitWait(int round, BlockHash hash, CancellationToken cancellationToken)
    {
        if (!_preCommitWaitFlags.Add(round))
        {
            return;
        }

        if (_options.EnterPreCommitDelay > 0)
        {
            await Task.Delay(
                _options.EnterPreCommitDelay,
                _cancellationTokenSource.Token);
        }

        await _dispatcher.InvokeAsync(_ => EnterPreCommit(round, hash), cancellationToken);
    }

    private async Task EnterEndCommitWait(int round, CancellationToken cancellationToken)
    {
        if (!_endCommitWaitFlags.Add(round))
        {
            return;
        }

        if (_options.EnterEndCommitDelay > 0)
        {
            await Task.Delay(
                _options.EnterEndCommitDelay,
                _cancellationTokenSource.Token);
        }

        await _dispatcher.InvokeAsync(_ => EnterEndCommit(round), cancellationToken);
    }

    private async Task OnTimeoutPropose(int round)
    {
        var timeout = _options.TimeoutPropose(round);
        await Task.Delay(timeout, _cancellationTokenSource.Token);
        await _dispatcher.InvokeAsync(_ => ProcessTimeoutPropose(round), _cancellationTokenSource.Token);
    }

    private async Task OnTimeoutPreVote(int round)
    {
        if (_preCommitTimeoutFlags.Contains(round) || !_preVoteTimeoutFlags.Add(round))
        {
            return;
        }

        var timeout = _options.TimeoutPreVote(round);
        await Task.Delay(timeout, _cancellationTokenSource.Token);
        await _dispatcher.InvokeAsync(_ => ProcessTimeoutPreVote(round), _cancellationTokenSource.Token);
    }

    private async Task OnTimeoutPreCommit(int round)
    {
        if (!_preCommitTimeoutFlags.Add(round))
        {
            return;
        }

        var timeout = _options.TimeoutPreCommit(round);
        await Task.Delay(timeout, _cancellationTokenSource.Token);
        await _dispatcher.InvokeAsync(_ => ProcessTimeoutPreCommit(round), _cancellationTokenSource.Token);
    }
}
