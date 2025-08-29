using System.Reactive.Subjects;
using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Data.Structures;
using Libplanet.Types;

namespace Libplanet.State;

public sealed class BlockExecutor(StateIndex states, SystemActions systemActions)
{
    private readonly Subject<ActionExecutionInfo> _actionExecutedSubject = new();
    private readonly Subject<TransactionExecutionInfo> _txExecutedResult = new();
    private readonly Subject<BlockExecutionInfo> _blockExecutedResult = new();

    public BlockExecutor(StateIndex states)
        : this(states, SystemActions.Empty)
    {
    }

    public IObservable<ActionExecutionInfo> ActionExecuted => _actionExecutedSubject;

    public IObservable<TransactionExecutionInfo> TransactionExecuted => _txExecutedResult;

    public IObservable<BlockExecutionInfo> BlockExecuted => _blockExecutedResult;

    public static int GenerateRandomSeed(ReadOnlySpan<byte> rawHashBytes, in ImmutableArray<byte> signature)
    {
        unchecked
        {
            if (rawHashBytes.Length <= 0)
            {
                throw new ArgumentException(
                    $"Given {nameof(rawHashBytes)} cannot be empty", nameof(rawHashBytes));
            }

            if (signature.Any())
            {
                return BitConverter.ToInt32(rawHashBytes) ^ BitConverter.ToInt32(SHA1.HashData([.. signature]), 0);
            }

            return BitConverter.ToInt32(rawHashBytes);
        }
    }

    public BlockExecutionInfo Execute(RawBlock rawBlock)
    {
        var world = new World(states, rawBlock.Header.PreviousStateRootHash);
        var inputWorld = world;
        var beginEvaluations = ExecuteActions(rawBlock, null, systemActions.EnterBlockActions, ref world);
        var txEvaluations = ExecuteTransactions(rawBlock, ref world);
        var endEvaluations = ExecuteActions(rawBlock, null, systemActions.LeaveBlockActions, ref world);

        var blockEvaluation = new BlockExecutionInfo
        {
            Block = rawBlock,
            EnterWorld = inputWorld,
            LeaveWorld = world,
            EnterExecutions = beginEvaluations,
            Executions = txEvaluations,
            LeaveExecutions = endEvaluations,
        };
        _blockExecutedResult.OnNext(blockEvaluation);
        return blockEvaluation;
    }

    private ImmutableArray<ActionExecutionInfo> ExecuteActions(
        RawBlock rawBlock, Transaction? tx, ImmutableArray<IAction> actions, ref World world)
    {
        var signature = tx?.Signature ?? [];
        var randomSeed = GenerateRandomSeed(rawBlock.Hash.Bytes.AsSpan(), signature);
        var evaluations = new ActionExecutionInfo[actions.Length];

        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var actionContext = new ActionContext
            {
                Signer = tx?.Signer ?? default,
                TxId = tx?.Id ?? default,
                Proposer = rawBlock.Header.Proposer,
                BlockHeight = rawBlock.Header.Height,
                BlockProtocolVersion = rawBlock.Header.BlockVersion,
                PreviousCommit = rawBlock.Header.PreviousBlockCommit,
                RandomSeed = randomSeed,
                MaxGasPrice = tx?.MaxGasPrice ?? default,
            };
            var evaluation = ExecuteAction(action, world, actionContext);
            evaluations[i] = evaluation;
            world = evaluation.LeaveWorld;

            unchecked
            {
                randomSeed++;
            }
        }

        return [.. evaluations];
    }

    private ActionExecutionInfo ExecuteAction(IAction action, World world, ActionContext actionContext)
    {
        if (!world.Trie.IsCommitted && !world.Trie.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Given {nameof(actionContext)} must have its previous state's " +
                $"{nameof(Trie)} recorded.");
        }

        var inputWorld = world;
        Exception? exception = null;

        try
        {
            using var worldContext = new WorldContext(world);
            action.Execute(worldContext, actionContext);
            world = worldContext.Flush();
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception e)
        {
            exception = e;
        }

        world = world.Commit();

        if (!world.Trie.IsCommitted)
        {
            throw new InvalidOperationException($"Failed to record {nameof(Account)}'s {nameof(Trie)}.");
        }

        var evaluation = new ActionExecutionInfo
        {
            Action = action,
            ActionContext = actionContext,
            EnterWorld = inputWorld,
            LeaveWorld = world,
            Exception = exception,
        };

        _actionExecutedSubject.OnNext(evaluation);
        return evaluation;
    }

    private ImmutableArray<TransactionExecutionInfo> ExecuteTransactions(RawBlock rawBlock, ref World world)
    {
        var txs = rawBlock.Content.Transactions;
        var capacity = GetCapacity(rawBlock);
        var evaluationList = new List<TransactionExecutionInfo>(capacity);

        foreach (var tx in txs)
        {
            evaluationList.Add(ExecuteTransaction(rawBlock, tx, ref world));
        }

        return [.. evaluationList];
    }

    private TransactionExecutionInfo ExecuteTransaction(RawBlock rawBlock, Transaction transaction, ref World world)
    {
        GasTracer.Initialize(transaction.GasLimit is 0 ? long.MaxValue : transaction.GasLimit);
        var inputWorld = world;
        GasTracer.IsTxAction = true;
        var beginEvaluations = ExecuteActions(rawBlock, transaction, systemActions.EnterTxActions, ref world);
        GasTracer.IsTxAction = false;
        var actions = transaction.Actions.Select(item => item.ToAction<IAction>()).ToImmutableArray();
        var evaluations = ExecuteActions(rawBlock, transaction, actions, ref world);
        GasTracer.IsTxAction = true;
        var endEvaluations = ExecuteActions(rawBlock, transaction, systemActions.LeaveTxActions, ref world);
        GasTracer.IsTxAction = false;

        GasTracer.Release();
        var txEvaluation = new TransactionExecutionInfo
        {
            Transaction = transaction,
            EnterWorld = inputWorld,
            LeaveWorld = world,
            EnterExecutions = beginEvaluations,
            Executions = evaluations,
            LeaveExecutions = endEvaluations,
        };

        _txExecutedResult.OnNext(txEvaluation);
        return txEvaluation;
    }

    private int GetCapacity(RawBlock rawBlock)
    {
        var txCount = rawBlock.Content.Transactions.Count;
        var actionCount = rawBlock.Content.Transactions.Sum(tx => tx.Actions.Length);
        var blockActionCount = systemActions.EnterBlockActions.Length
            + systemActions.LeaveBlockActions.Length;
        var txActionCount = systemActions.EnterTxActions.Length
            + systemActions.LeaveTxActions.Length;
        var capacity = actionCount + blockActionCount + (txActionCount * txCount);
        return capacity;
    }
}
