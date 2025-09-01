using System.Reactive.Subjects;
using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.State.Structures;
using Libplanet.Types;

namespace Libplanet.State;

public sealed class BlockExecutor(StateIndex states, SystemActions systemActions)
{
    private readonly Subject<ActionExecution> _actionExecutedSubject = new();
    private readonly Subject<TransactionExecution> _txExecutedResult = new();
    private readonly Subject<BlockExecution> _blockExecutedResult = new();

    public BlockExecutor(StateIndex states)
        : this(states, SystemActions.Empty)
    {
    }

    public IObservable<ActionExecution> ActionExecuted => _actionExecutedSubject;

    public IObservable<TransactionExecution> TransactionExecuted => _txExecutedResult;

    public IObservable<BlockExecution> BlockExecuted => _blockExecutedResult;

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

    public BlockExecution Execute(Block block)
    {
        var world = new World(states, block.Header.PreviousStateRootHash);
        var inputWorld = world;
        var enterEvaluations = ExecuteActions(block, null, systemActions.EnterBlockActions, ref world);
        var txEvaluations = ExecuteTransactions(block, ref world);
        var leaveEvaluations = ExecuteActions(block, null, systemActions.LeaveBlockActions, ref world);

        var blockEvaluation = new BlockExecution
        {
            Block = block,
            EnterWorld = inputWorld,
            LeaveWorld = world,
            EnterExecutions = enterEvaluations,
            Executions = txEvaluations,
            LeaveExecutions = leaveEvaluations,
        };
        _blockExecutedResult.OnNext(blockEvaluation);
        return blockEvaluation;
    }

    private ImmutableArray<ActionExecution> ExecuteActions(
        Block block, Transaction? tx, ImmutableArray<IAction> actions, ref World world)
    {
        var signature = tx?.Signature ?? [];
        var randomSeed = GenerateRandomSeed(block.BlockHash.Bytes.AsSpan(), signature);
        var evaluations = new ActionExecution[actions.Length];

        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var actionContext = new ActionContext
            {
                Signer = tx?.Signer ?? default,
                TxId = tx?.Id ?? default,
                Proposer = block.Header.Proposer,
                BlockHeight = block.Header.Height,
                BlockProtocolVersion = block.Header.BlockVersion,
                PreviousCommit = block.Header.PreviousBlockCommit,
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

    private ActionExecution ExecuteAction(IAction action, World world, ActionContext actionContext)
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

        var evaluation = new ActionExecution
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

    private ImmutableArray<TransactionExecution> ExecuteTransactions(Block block, ref World world)
    {
        var txs = block.Content.Transactions;
        var capacity = GetCapacity(block);
        var evaluationList = new List<TransactionExecution>(capacity);

        foreach (var tx in txs)
        {
            evaluationList.Add(ExecuteTransaction(block, tx, ref world));
        }

        return [.. evaluationList];
    }

    private TransactionExecution ExecuteTransaction(Block block, Transaction transaction, ref World world)
    {
        GasTracer.Initialize(transaction.GasLimit is 0 ? long.MaxValue : transaction.GasLimit);
        var inputWorld = world;
        GasTracer.IsTxAction = true;
        var enterEvaluations = ExecuteActions(block, transaction, systemActions.EnterTxActions, ref world);
        GasTracer.IsTxAction = false;
        var actions = transaction.Actions.Select(item => item.ToAction<IAction>()).ToImmutableArray();
        var evaluations = ExecuteActions(block, transaction, actions, ref world);
        GasTracer.IsTxAction = true;
        var leaveEvaluations = ExecuteActions(block, transaction, systemActions.LeaveTxActions, ref world);
        GasTracer.IsTxAction = false;

        GasTracer.Release();
        var txEvaluation = new TransactionExecution
        {
            Transaction = transaction,
            EnterWorld = inputWorld,
            LeaveWorld = world,
            EnterExecutions = enterEvaluations,
            Executions = evaluations,
            LeaveExecutions = leaveEvaluations,
        };

        _txExecutedResult.OnNext(txEvaluation);
        return txEvaluation;
    }

    private int GetCapacity(Block block)
    {
        var txCount = block.Content.Transactions.Count;
        var actionCount = block.Content.Transactions.Sum(tx => tx.Actions.Length);
        var blockActionCount = systemActions.EnterBlockActions.Length
            + systemActions.LeaveBlockActions.Length;
        var txActionCount = systemActions.EnterTxActions.Length
            + systemActions.LeaveTxActions.Length;
        var capacity = actionCount + blockActionCount + (txActionCount * txCount);
        return capacity;
    }
}
