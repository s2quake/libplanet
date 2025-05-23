using System.Reactive.Subjects;
using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Data.Structures;
using Libplanet.Types.Blocks;
using Libplanet.Types.Transactions;

namespace Libplanet.Action;

public sealed class BlockExecutor(StateStore stateStore, SystemActions systemActions)
{
    private readonly Subject<ActionResult> _actionExecutedSubject = new();
    private readonly Subject<TransactionResult> _txExecutedResult = new();
    private readonly Subject<BlockResult> _blockExecutedResult = new();

    public BlockExecutor(StateStore stateStore)
        : this(stateStore, SystemActions.Empty)
    {
    }

    public IObservable<ActionResult> ActionExecuted => _actionExecutedSubject;

    public IObservable<TransactionResult> TransactionExecuted => _txExecutedResult;

    public IObservable<BlockResult> BlockExecuted => _blockExecutedResult;

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

    public BlockResult Execute(RawBlock rawBlock)
    {
        var world = new World(stateStore, rawBlock.Header.PreviousStateRootHash);
        var inputWorld = world;
        var beginEvaluations = ExecuteActions(rawBlock, null, systemActions.BeginBlockActions, ref world);
        var txEvaluations = ExecuteTransactions(rawBlock, ref world);
        var endEvaluations = ExecuteActions(rawBlock, null, systemActions.EndBlockActions, ref world);

        var blockEvaluation = new BlockResult
        {
            Block = rawBlock,
            InputWorld = inputWorld,
            OutputWorld = world,
            BeginEvaluations = beginEvaluations,
            Evaluations = txEvaluations,
            EndEvaluations = endEvaluations,
        };
        _blockExecutedResult.OnNext(blockEvaluation);
        return blockEvaluation;
    }

    private ImmutableArray<ActionResult> ExecuteActions(
        RawBlock rawBlock, Transaction? tx, ImmutableArray<IAction> actions, ref World world)
    {
        var signature = tx?.Signature ?? [];
        var randomSeed = GenerateRandomSeed(rawBlock.Hash.Bytes.AsSpan(), signature);
        var evaluations = new ActionResult[actions.Length];

        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var actionContext = new ActionContext
            {
                Signer = tx?.Signer ?? default,
                TxId = tx?.Id ?? default,
                Proposer = rawBlock.Header.Proposer,
                BlockHeight = rawBlock.Header.Height,
                BlockProtocolVersion = rawBlock.Header.Version,
                LastCommit = rawBlock.Header.PreviousCommit,
                RandomSeed = randomSeed,
                MaxGasPrice = tx?.MaxGasPrice ?? default,
            };
            var evaluation = ExecuteAction(action, world, actionContext);
            evaluations[i] = evaluation;
            world = evaluation.OutputWorld;

            unchecked
            {
                randomSeed++;
            }
        }

        return [.. evaluations];
    }

    private ActionResult ExecuteAction(IAction action, World world, ActionContext actionContext)
    {
        if (!world.Trie.IsCommitted)
        {
            throw new InvalidOperationException(
                $"Given {nameof(actionContext)} must have its previous state's " +
                $"{nameof(ITrie)} recorded.");
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
            throw new InvalidOperationException(
                $"Failed to record {nameof(Account)}'s {nameof(ITrie)}.");
        }

        var evaluation = new ActionResult
        {
            Action = action,
            InputContext = actionContext,
            InputWorld = inputWorld,
            OutputWorld = world,
            Exception = exception,
        };

        _actionExecutedSubject.OnNext(evaluation);
        return evaluation;
    }

    private ImmutableArray<TransactionResult> ExecuteTransactions(RawBlock rawBlock, ref World world)
    {
        var txs = rawBlock.Content.Transactions;
        var capacity = GetCapacity(rawBlock);
        var evaluationList = new List<TransactionResult>(capacity);

        foreach (var tx in txs)
        {
            evaluationList.Add(ExecuteTransaction(rawBlock, tx, ref world));
        }

        return [.. evaluationList];
    }

    private TransactionResult ExecuteTransaction(RawBlock rawBlock, Transaction transaction, ref World world)
    {
        GasTracer.Initialize(transaction.GasLimit is 0 ? long.MaxValue : transaction.GasLimit);
        var inputWorld = world;
        GasTracer.IsTxAction = true;
        var beginEvaluations = ExecuteActions(rawBlock, transaction, systemActions.BeginTxActions, ref world);
        GasTracer.IsTxAction = false;
        var actions = transaction.Actions.Select(item => item.ToAction<IAction>()).ToImmutableArray();
        var evaluations = ExecuteActions(rawBlock, transaction, actions, ref world);
        GasTracer.IsTxAction = true;
        var endEvaluations = ExecuteActions(rawBlock, transaction, systemActions.EndTxActions, ref world);
        GasTracer.IsTxAction = false;

        GasTracer.Release();
        var txEvaluation = new TransactionResult
        {
            Transaction = transaction,
            InputWorld = inputWorld,
            OutputWorld = world,
            BeginEvaluations = beginEvaluations,
            Evaluations = evaluations,
            EndEvaluations = endEvaluations,
        };

        _txExecutedResult.OnNext(txEvaluation);
        return txEvaluation;
    }

    private int GetCapacity(RawBlock rawBlock)
    {
        var txCount = rawBlock.Content.Transactions.Count;
        var actionCount = rawBlock.Content.Transactions.Sum(tx => tx.Actions.Length);
        var blockActionCount = systemActions.BeginBlockActions.Length
            + systemActions.EndBlockActions.Length;
        var txActionCount = systemActions.BeginTxActions.Length
            + systemActions.EndTxActions.Length;
        var capacity = actionCount + blockActionCount + (txActionCount * txCount);
        return capacity;
    }
}
