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
    public ActionEvaluator(TrieStateStore stateStore)
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

    public ActionEvaluation[] Evaluate(RawBlock rawBlock, HashDigest<SHA256> baseStateRootHash)
    {
        var world = stateStore.GetWorld(baseStateRootHash);
        int capacity = GetCapacity(rawBlock);
        var evaluationsList = new List<ActionEvaluation>(capacity);
        if (policyActions.BeginBlockActions.Length > 0)
        {
            evaluationsList.AddRange(EvaluateBeginBlockActions(rawBlock, world));
            world = evaluationsList[^1].OutputWorld;
        }

        evaluationsList.AddRange([.. EvaluateBlock(rawBlock, world)]);

        if (policyActions.EndBlockActions.Length > 0)
        {
            world = evaluationsList.Count > 0 ? evaluationsList[^1].OutputWorld : world;
            evaluationsList.AddRange(EvaluateEndBlockActions(rawBlock, world));
        }

        return [.. evaluationsList];
    }

    internal ActionEvaluation[] EvaluateActions(
        RawBlock rawBlock, Transaction? tx, World world, ImmutableArray<ActionBytecode> actions)
    {
        var builder = ImmutableArray.CreateBuilder<IAction>(actions.Length);
        for (var i = 0; i < actions.Length; i++)
        {
            builder.Add(actions[i].ToAction<IAction>());
        }

        return EvaluateActions(rawBlock, tx, world, builder.ToImmutable());
    }

    internal ActionEvaluation[] EvaluateActions(
        RawBlock rawBlock, Transaction? tx, World world, ImmutableArray<IAction> actions)
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

        return evaluations;
    }

    internal ActionEvaluation EvaluateAction(IAction action, World world, ActionContext actionContext)
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

        return new ActionEvaluation
        {
            Action = action,
            InputContext = actionContext,
            InputWorld = inputWorld,
            OutputWorld = world,
            Exception = exception,
        };
    }

    internal ActionEvaluation[] EvaluateBlock(RawBlock rawBlock, World world)
    {
        var txs = rawBlock.Content.Transactions;
        var capacity = GetCapacity(rawBlock);
        var evaluationList = new List<ActionEvaluation>(capacity);

        foreach (var tx in txs)
        {
            var evaluations = EvaluateTx(rawBlock, tx, world);
            foreach (var evaluation in evaluations)
            {
                evaluationList.Add(evaluation);
                world = evaluation.OutputWorld;
            }
        }

        return [.. evaluationList];
    }

    internal ActionEvaluation[] EvaluateTx(RawBlock rawBlock, Transaction tx, World world)
    {
        GasTracer.Initialize(tx.GasLimit ?? long.MaxValue);
        var evaluationList = new List<ActionEvaluation>();
        if (policyActions.BeginTxActions.Length > 0)
        {
            GasTracer.IsTxAction = true;
            evaluationList.AddRange(EvaluateBeginTxActions(rawBlock, tx, world));
            world = evaluationList[^1].OutputWorld;
            GasTracer.IsTxAction = false;
        }

        var actions = tx.Actions.Select(item => item.ToAction<IAction>()).ToImmutableArray();
        evaluationList.AddRange(EvaluateActions(rawBlock, tx, world, actions));

        if (policyActions.EndTxActions.Length > 0)
        {
            GasTracer.IsTxAction = true;
            world = evaluationList.Count > 0 ? evaluationList[^1].OutputWorld : world;
            evaluationList.AddRange(EvaluateEndTxActions(rawBlock, tx, world));
            GasTracer.IsTxAction = false;
        }

        GasTracer.Release();

        return [.. evaluationList];
    }

    internal ActionEvaluation[] EvaluateBeginBlockActions(RawBlock rawBlock, World world)
    {
        return EvaluateActions(rawBlock, null, world, policyActions.BeginBlockActions);
    }

    internal ActionEvaluation[] EvaluateEndBlockActions(RawBlock rawBlock, World world)
    {
        return EvaluateActions(rawBlock, null, world, policyActions.EndBlockActions);
    }

    internal ActionEvaluation[] EvaluateBeginTxActions(RawBlock rawBlock, Transaction tx, World world)
    {
        return EvaluateActions(rawBlock, tx, world, policyActions.BeginTxActions);
    }

    internal ActionEvaluation[] EvaluateEndTxActions(RawBlock rawBlock, Transaction tx, World world)
    {
        return EvaluateActions(rawBlock, tx, world, policyActions.EndTxActions);
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
