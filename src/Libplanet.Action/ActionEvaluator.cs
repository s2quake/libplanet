using System.Reactive.Subjects;
using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public sealed class ActionEvaluator(TrieStateStore stateStore, PolicyActions policyActions)
{
    private readonly Subject<ActionEvaluation> _evaluation = new();
    private readonly Subject<TxEvaluation> _txEvaluation = new();
    private readonly Subject<BlockEvaluation> _blockEvaluation = new();

    public ActionEvaluator(TrieStateStore stateStore)
        : this(stateStore, PolicyActions.Empty)
    {
    }

    public IObservable<ActionEvaluation> ActionEvaluated => _evaluation;

    public IObservable<TxEvaluation> TxEvaluated => _txEvaluation;

    public IObservable<BlockEvaluation> Evaluated => _blockEvaluation;

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

            return BitConverter.ToInt32(rawHashBytes) ^ 0;
        }
    }

    public BlockEvaluation Evaluate(RawBlock rawBlock, HashDigest<SHA256> baseStateRootHash)
    {
        var world = stateStore.GetWorld(baseStateRootHash);
        var inputWorld = world;
        var beginEvaluations = EvaluateActions(rawBlock, null, policyActions.BeginBlockActions, ref world);
        var txEvaluations = EvaluateTxs(rawBlock, ref world);
        var endEvaluations = EvaluateActions(rawBlock, null, policyActions.EndBlockActions, ref world);

        var blockEvaluation = new BlockEvaluation
        {
            Block = rawBlock,
            InputWorld = inputWorld,
            OutputWorld = world,
            BeginEvaluations = beginEvaluations,
            Evaluations = txEvaluations,
            EndEvaluations = endEvaluations,
        };
        _blockEvaluation.OnNext(blockEvaluation);
        return blockEvaluation;
    }

    private ImmutableArray<ActionEvaluation> EvaluateActions(
        RawBlock rawBlock, Transaction? tx, ImmutableArray<IAction> actions, ref World world)
    {
        var signature = tx?.Signature ?? [];
        var randomSeed = GenerateRandomSeed(rawBlock.Hash.Bytes.AsSpan(), signature);
        var evaluations = new ActionEvaluation[actions.Length];

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
                LastCommit = rawBlock.Header.LastCommit,
                RandomSeed = randomSeed,
                MaxGasPrice = tx?.MaxGasPrice ?? default,
            };
            var evaluation = EvaluateAction(action, world, actionContext);
            evaluations[i] = evaluation;
            world = evaluation.OutputWorld;

            unchecked
            {
                randomSeed++;
            }
        }

        return [.. evaluations];
    }

    private ActionEvaluation EvaluateAction(IAction action, World world, ActionContext actionContext)
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

        world = stateStore.CommitWorld(world);

        if (!world.Trie.IsCommitted)
        {
            throw new InvalidOperationException(
                $"Failed to record {nameof(Account)}'s {nameof(ITrie)}.");
        }

        var evaluation = new ActionEvaluation
        {
            Action = action,
            InputContext = actionContext,
            InputWorld = inputWorld,
            OutputWorld = world,
            Exception = exception,
        };

        _evaluation.OnNext(evaluation);
        return evaluation;
    }

    private ImmutableArray<TxEvaluation> EvaluateTxs(RawBlock rawBlock, ref World world)
    {
        var txs = rawBlock.Content.Transactions;
        var capacity = GetCapacity(rawBlock);
        var evaluationList = new List<TxEvaluation>(capacity);

        foreach (var tx in txs)
        {
            evaluationList.Add(EvaluateTx(rawBlock, tx, ref world));
        }

        return [.. evaluationList];
    }

    private TxEvaluation EvaluateTx(RawBlock rawBlock, Transaction tx, ref World world)
    {
        GasTracer.Initialize(tx.GasLimit is 0 ? long.MaxValue : tx.GasLimit);
        var inputWorld = world;
        GasTracer.IsTxAction = true;
        var beginEvaluations = EvaluateActions(rawBlock, tx, policyActions.BeginTxActions, ref world);
        GasTracer.IsTxAction = false;
        var actions = tx.Actions.Select(item => item.ToAction<IAction>()).ToImmutableArray();
        var evaluations = EvaluateActions(rawBlock, tx, actions, ref world);
        GasTracer.IsTxAction = true;
        var endEvaluations = EvaluateActions(rawBlock, tx, policyActions.EndTxActions, ref world);
        GasTracer.IsTxAction = false;

        GasTracer.Release();
        var txEvaluation = new TxEvaluation
        {
            Transaction = tx,
            InputWorld = inputWorld,
            OutputWorld = world,
            BeginEvaluations = beginEvaluations,
            Evaluations = evaluations,
            EndEvaluations = endEvaluations,
        };

        _txEvaluation.OnNext(txEvaluation);
        return txEvaluation;
    }

    private int GetCapacity(RawBlock rawBlock)
    {
        var txCount = rawBlock.Content.Transactions.Count;
        var actionCount = rawBlock.Content.Transactions.Sum(tx => tx.Actions.Length);
        var blockActionCount = policyActions.BeginBlockActions.Length
            + policyActions.EndBlockActions.Length;
        var txActionCount = policyActions.BeginTxActions.Length
            + policyActions.EndTxActions.Length;
        var capacity = actionCount + blockActionCount + (txActionCount * txCount);
        return capacity;
    }
}
