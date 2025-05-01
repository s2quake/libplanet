using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public sealed class ActionEvaluator(IStateStore stateStore, PolicyActions policyActions)
{
    public ActionEvaluator(IStateStore stateStore)
        : this(stateStore, PolicyActions.Empty)
    {
    }

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

    public CommittedActionEvaluation[] Evaluate(RawBlock block, HashDigest<SHA256> baseStateRootHash)
    {
        try
        {
            var world = stateStore.GetWorld(baseStateRootHash);
            int capacity = GetCapacity(block);
            var evaluationsList = new List<ActionEvaluation>(capacity);
            if (policyActions.BeginBlockActions.Length > 0)
            {
                evaluationsList.AddRange(EvaluateBeginBlockActions(block, world));
                world = evaluationsList.Last().OutputState;
            }

            evaluationsList.AddRange([.. EvaluateBlock(block, world)]);

            if (policyActions.EndBlockActions.Length > 0)
            {
                world = evaluationsList.Count > 0 ? evaluationsList.Last().OutputState : world;
                evaluationsList.AddRange(EvaluateEndBlockActions(block, world));
            }

            return [.. evaluationsList.Select(item => (CommittedActionEvaluation)item)];
        }
        catch (Exception)
        {
            throw;
        }
    }

    internal ActionEvaluation[] EvaluateActions(
        RawBlock block, Transaction? tx, World world, ImmutableArray<IAction> actions)
    {
        var signature = tx?.Signature ?? [];
        var randomSeed = GenerateRandomSeed(block.RawHash.Bytes.AsSpan(), signature);
        var evaluations = new ActionEvaluation[actions.Length];

        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var actionContext = new ActionContext
            {
                Signer = tx?.Signer ?? default,
                TxId = tx?.Id ?? default,
                Proposer = block.Proposer,
                BlockHeight = block.Height,
                BlockProtocolVersion = block.ProtocolVersion,
                LastCommit = block.LastCommit,
                Txs = block.Transactions,
                World = world,
                RandomSeed = randomSeed,
                MaxGasPrice = tx?.MaxGasPrice ?? default,
                Evidence = block.Evidence,
            };
            var evaluation = EvaluateAction(actionContext, action);
            evaluations[i] = evaluation;
            world = evaluation.OutputState;

            unchecked { randomSeed++; }
        }

        return evaluations;
    }

    internal ActionEvaluation EvaluateAction(ActionContext context, IAction action)
    {
        if (!context.World.Trie.IsCommitted)
        {
            throw new InvalidOperationException(
                $"Given {nameof(context)} must have its previous state's " +
                $"{nameof(ITrie)} recorded.");
        }

        var world = context.World;
        Exception? exception = null;

        try
        {
            world = action.Execute(context);
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

        return new ActionEvaluation
        {
            Action = action,
            InputContext = context,
            OutputState = world,
            Exception = exception,
        };
    }

    internal ActionEvaluation[] EvaluateBlock(RawBlock block, World world)
    {
        var txs = block.Transactions;
        var capacity = GetCapacity(block);
        var evaluationList = new List<ActionEvaluation>(capacity);

        foreach (var tx in txs)
        {
            var evaluations = EvaluateTx(block, tx, world);
            foreach (var evaluation in evaluations)
            {
                evaluationList.Add(evaluation);
                world = evaluation.OutputState;
            }
        }

        return [.. evaluationList];
    }

    internal ActionEvaluation[] EvaluateTx(RawBlock block, Transaction tx, World world)
    {
        GasTracer.Initialize(tx.GasLimit ?? long.MaxValue);
        var evaluationList = new List<ActionEvaluation>();
        if (policyActions.BeginTxActions.Length > 0)
        {
            GasTracer.IsTxAction = true;
            evaluationList.AddRange(EvaluateBeginTxActions(block, tx, world));
            world = evaluationList.Last().OutputState;
            GasTracer.IsTxAction = false;
        }

        var actions = tx.Actions.Select(ModelSerializer.Deserialize<IAction>).ToImmutableArray();
        evaluationList.AddRange(EvaluateActions(block, tx, world, actions));

        if (policyActions.EndTxActions.Length > 0)
        {
            GasTracer.IsTxAction = true;
            world = evaluationList.Count > 0 ? evaluationList.Last().OutputState : world;
            evaluationList.AddRange(EvaluateEndTxActions(block, tx, world));
            GasTracer.IsTxAction = false;
        }

        GasTracer.Release();

        return [.. evaluationList];
    }

    internal ActionEvaluation[] EvaluateBeginBlockActions(RawBlock block, World world)
    {
        return EvaluateActions(block, null, world, policyActions.BeginBlockActions);
    }

    internal ActionEvaluation[] EvaluateEndBlockActions(RawBlock block, World world)
    {
        return EvaluateActions(block, null, world, policyActions.EndBlockActions);
    }

    internal ActionEvaluation[] EvaluateBeginTxActions(RawBlock block, Transaction tx, World world)
    {
        return EvaluateActions(block, tx, world, policyActions.BeginTxActions);
    }

    internal ActionEvaluation[] EvaluateEndTxActions(RawBlock block, Transaction tx, World world)
    {
        return EvaluateActions(block, tx, world, policyActions.EndTxActions);
    }

    private int GetCapacity(RawBlock block)
    {
        var txCount = block.Transactions.Count;
        var actionCount = block.Transactions.Sum(tx => tx.Actions.Length);
        var blockActionCount = policyActions.BeginBlockActions.Length
            + policyActions.EndBlockActions.Length;
        var txActionCount = policyActions.BeginTxActions.Length
            + policyActions.EndTxActions.Length;
        var capacity = actionCount + blockActionCount + (txActionCount * txCount);
        return capacity;
    }
}
