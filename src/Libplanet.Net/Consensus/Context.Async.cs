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

        _startedSubject.OnNext(Height);
        await _dispatcher.InvokeAsync(_ => StartRound(0), _cancellationTokenSource.Token);

        // FIXME: Exceptions inside tasks should be handled properly.
        _ = MessageConsumerTask(_cancellationTokenSource.Token);
        // _ = MutationConsumerTask(_cancellationTokenSource.Token);
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
                    if (AddMessage(message))
                    {
                        ProcessHeightOrRoundUponRules(message);
                    }
                }, cancellationToken);

                MessageConsumed?.Invoke(this, message);
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
        _ = _messageRequests.Writer.WriteAsync(message);
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

    private async Task EnterPreCommitWait(int round, BlockHash blockHash, CancellationToken cancellationToken)
    {
        if (!_preCommitWaitFlags.Add(round))
        {
            return;
        }

        await Task.Delay(_options.EnterPreCommitDelay, cancellationToken);
        await _dispatcher.InvokeAsync(_ => EnterPreCommit(round, blockHash), cancellationToken);
    }

    private async Task EnterEndCommitWait(int round, CancellationToken cancellationToken)
    {
        if (!_endCommitWaitFlags.Add(round))
        {
            return;
        }

        await Task.Delay(_options.EnterEndCommitDelay, cancellationToken);
        await _dispatcher.InvokeAsync(_ => EnterEndCommit(round), cancellationToken);
    }

    private async Task OnTimeoutProposeAsync(int round, CancellationToken cancellationToken)
    {
        var timeout = _options.TimeoutPropose(round);
        await Task.Delay(timeout, cancellationToken);
        await _dispatcher.InvokeAsync(_ =>
        {
            if (round == Round && Step == ConsensusStep.Propose)
            {
                EnterPreVote(round, default);
                TimeoutProcessed?.Invoke(this, (round, ConsensusStep.Propose));
            }
        }, cancellationToken);
    }

    private async Task OnTimeoutPreVoteAsync(int round, CancellationToken cancellationToken)
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
                TimeoutProcessed?.Invoke(this, (round, ConsensusStep.PreVote));
            }
        }, cancellationToken);
    }

    private async Task OnTimeoutPreCommitAsync(int round, CancellationToken cancellationToken)
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
                TimeoutProcessed?.Invoke(this, (round, ConsensusStep.PreCommit));
            }
        }, cancellationToken);
    }
}

