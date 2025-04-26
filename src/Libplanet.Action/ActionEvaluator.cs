using System.Diagnostics;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action.Loader;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using Serilog;

namespace Libplanet.Action;

public sealed class ActionEvaluator(
    PolicyActionsRegistry policyActionsRegistry,
    IStateStore stateStore,
    IActionLoader actionLoader)
{
    private readonly ILogger _logger = Log.ForContext<ActionEvaluator>()
        .ForContext("Source", nameof(ActionEvaluator));

    private delegate (ITrie, int) StateCommitter(
        ITrie worldTrie, ActionEvaluation evaluation);

    public IActionLoader ActionLoader { get; } = actionLoader;

    public static int GenerateRandomSeed(
        byte[] preEvaluationHashBytes, in ImmutableArray<byte> signature, int actionOffset)
    {
        using var sha1 = SHA1.Create();
        unchecked
        {
            return ((preEvaluationHashBytes.Length > 0
                ? BitConverter.ToInt32(preEvaluationHashBytes, 0)
                : throw new ArgumentException(
                    $"Given {nameof(preEvaluationHashBytes)} cannot be empty",
                    nameof(preEvaluationHashBytes)))
            ^ (signature.Any()
                ? BitConverter.ToInt32(sha1.ComputeHash([.. signature]), 0)
                : 0))
            + actionOffset;
        }
    }

    public CommittedActionEvaluation[] Evaluate(
        RawBlock block, HashDigest<SHA256> baseStateRootHash)
    {
        _logger.Information(
            "Evaluating actions in the block #{BlockHeight} " +
            "pre-evaluation hash {RawHash}...",
            block.Height,
            ByteUtil.Hex(block.RawHash.Bytes));
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        try
        {
            var world = stateStore.GetWorld(baseStateRootHash);
            int capacity = GetCapacity(block);
            var evaluationsList = new List<ActionEvaluation>(capacity);
            if (policyActionsRegistry.BeginBlockActions.Length > 0)
            {
                evaluationsList.AddRange(EvaluateBeginBlockActions(block, world));
                world = evaluationsList.Last().OutputState;
            }

            evaluationsList.AddRange([.. EvaluateBlock(block, world)]);

            if (policyActionsRegistry.EndBlockActions.Length > 0)
            {
                world = evaluationsList.Count > 0 ? evaluationsList.Last().OutputState : world;
                evaluationsList.AddRange(EvaluateEndBlockActions(block, world));
            }

            return [.. evaluationsList.Select(item => (CommittedActionEvaluation)item)];
        }
        catch (Exception e)
        {
            const string errorMessage =
                "Failed to evaluate block #{BlockHeight} pre-evaluation hash " +
                "pre-evaluation has {RawHash}";
            _logger.Error(
                e,
                errorMessage,
                block.Height,
                ByteUtil.Hex(block.RawHash.Bytes));
            throw;
        }
        finally
        {
            _logger
                .ForContext("Tag", "Metric")
                .ForContext("Subtag", "BlockEvaluationDuration")
                .Information(
                    "Actions in {TxCount} transactions for block #{BlockHeight} " +
                    "pre-evaluation hash {RawHash} evaluated in {DurationMs} ms",
                    block.Transactions.Count,
                    block.Height,
                    ByteUtil.Hex(block.RawHash.Bytes),
                    stopwatch.ElapsedMilliseconds);
        }
    }

    internal ActionEvaluation[] EvaluateActions(
        RawBlock block,
        Transaction? tx,
        IWorld world,
        ImmutableArray<IAction> actions,
        bool isPolicyAction)
    {
        byte[] preEvaluationHashBytes = block.RawHash.Bytes.ToArray();
        var signature = tx?.Signature ?? [];
        var randomSeed = GenerateRandomSeed(preEvaluationHashBytes, signature, 0);
        var evaluations = new ActionEvaluation[actions.Length];

        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var actionContext = new ActionContext
            {
                Signer = tx?.Signer ?? default,
                TxId = tx?.Id ?? null,
                Miner = block.Miner,
                BlockHeight = block.Height,
                BlockProtocolVersion = block.ProtocolVersion,
                LastCommit = block.LastCommit,
                Txs = block.Transactions,
                World = world,
                IsPolicyAction = isPolicyAction,
                RandomSeed = randomSeed,
                MaxGasPrice = tx?.MaxGasPrice,
                Evidence = block.Evidence,
            };
            var evaluation = EvaluateAction(actionContext, action);
            evaluations[i] = evaluation;
            world = evaluation.OutputState;

            unchecked
            {
                randomSeed++;
            }
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

        var inputContext = context;
        var world = inputContext.World;
        Exception? exception = null;

        try
        {
            context = inputContext with
            {
                World = world,
            };
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
                $"Failed to record {nameof(IAccount)}'s {nameof(ITrie)}.");
        }

        return new ActionEvaluation
        {
            Action = action,
            InputContext = inputContext,
            OutputState = world,
            Exception = exception,
        };
    }

    internal static IEnumerable<Transaction> OrderTxsForEvaluation(
        IEnumerable<Transaction> txs,
        ImmutableArray<byte> preEvaluationHashBytes)
    {
        return OrderTxsForEvaluationV3(txs, preEvaluationHashBytes);
    }

    internal IEnumerable<ActionEvaluation> EvaluateBlock(RawBlock block, IWorld previousState)
    {
        IWorld delta = previousState;
        IEnumerable<Transaction> txs = OrderTxsForEvaluation(
            block.Transactions,
            block.RawHash.Bytes);

        foreach (var tx in txs)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            IEnumerable<ActionEvaluation> evaluations = EvaluateTx(
                block: block,
                tx: tx,
                world: delta);

            var actions = new List<IAction>();
            foreach (ActionEvaluation evaluation in evaluations)
            {
                yield return evaluation;
                delta = evaluation.OutputState;
                actions.Add(evaluation.Action);
            }

            // FIXME: This is dependent on when the returned value is enumerated.
            ILogger logger = _logger
                .ForContext("Tag", "Metric")
                .ForContext("Subtag", "TxEvaluationDuration");
            logger.Information(
                "Took {DurationMs} ms to evaluate {ActionCount} actions {ActionTypes} " +
                "in transaction {TxId} by {Signer} as a part of block #{Index} " +
                "pre-evaluation hash {RawHash}",
                stopwatch.ElapsedMilliseconds,
                actions.Count,
                actions.Select(action => action.ToString()!.Split('.')
                    .LastOrDefault()?.Replace(">", string.Empty)),
                tx.Id,
                tx.Signer,
                block.Height,
                ByteUtil.Hex(block.RawHash.Bytes));
        }
    }

    internal ActionEvaluation[] EvaluateTx(RawBlock block, Transaction tx, IWorld world)
    {
        GasTracer.Initialize(tx.GasLimit ?? long.MaxValue);
        var evaluationList = new List<ActionEvaluation>();
        if (policyActionsRegistry.BeginTxActions.Length > 0)
        {
            GasTracer.IsTxAction = true;
            evaluationList.AddRange(EvaluateBeginTxActions(block, tx, world));
            world = evaluationList.Last().OutputState;
            GasTracer.IsTxAction = false;
        }

        var actions = LoadActions(block.Height, tx).ToImmutableArray();
        evaluationList.AddRange(EvaluateActions(
            block: block,
            tx: tx,
            world: world,
            actions: actions,
            isPolicyAction: false));

        if (policyActionsRegistry.EndTxActions.Length > 0)
        {
            GasTracer.IsTxAction = true;
            world = evaluationList.Count > 0 ? evaluationList.Last().OutputState : world;
            evaluationList.AddRange(EvaluateEndTxActions(block, tx, world));
            GasTracer.IsTxAction = false;
        }

        GasTracer.Release();

        return [.. evaluationList];
    }

    internal ActionEvaluation[] EvaluateBeginBlockActions(RawBlock block, IWorld world)
    {
        _logger.Information(
            "Evaluating policy begin block actions for block #{BlockHeight} {BlockHash}",
            block.Height,
            ByteUtil.Hex(block.RawHash.Bytes));

        return EvaluateActions(
            block: block,
            tx: null,
            world: world,
            actions: policyActionsRegistry.BeginBlockActions,
            isPolicyAction: true).ToArray();
    }

    internal ActionEvaluation[] EvaluateEndBlockActions(RawBlock block, IWorld world)
    {
        _logger.Information(
            $"Evaluating policy end block actions for block #{block.Height} " +
            $"{ByteUtil.Hex(block.RawHash.Bytes)}");

        return EvaluateActions(
            block: block,
            tx: null,
            world: world,
            actions: policyActionsRegistry.EndBlockActions,
            isPolicyAction: true).ToArray();
    }

    internal ActionEvaluation[] EvaluateBeginTxActions(
        RawBlock block, Transaction transaction, IWorld previousState)
    {
        _logger.Information(
            $"Evaluating policy begin tx actions for block #{block.Height} " +
            $"{ByteUtil.Hex(block.RawHash.Bytes)}");

        return EvaluateActions(
            block: block,
            tx: transaction,
            world: previousState,
            actions: policyActionsRegistry.BeginTxActions,
            isPolicyAction: true).ToArray();
    }

    internal ActionEvaluation[] EvaluateEndTxActions(RawBlock block, Transaction tx, IWorld world)
    {
        _logger.Information(
            $"Evaluating policy end tx actions for block #{block.Height} " +
            $"{ByteUtil.Hex(block.RawHash.Bytes)}");

        return EvaluateActions(
            block: block,
            tx: tx,
            world: world,
            actions: policyActionsRegistry.EndTxActions,
            isPolicyAction: true).ToArray();
    }

    // internal CommittedActionEvaluation[] ToCommittedEvaluation(
    //     RawBlock block,
    //     IReadOnlyList<ActionEvaluation> evaluations,
    //     HashDigest<SHA256>? baseStateRootHash)
    // {
    //     var committedEvaluations = new List<CommittedActionEvaluation>();
    //     foreach (var evaluation in evaluations)
    //     {
    //         committedEvaluations.Add((CommittedActionEvaluation)evaluation);
    //     }

    //     return committedEvaluations.ToArray();
    // }

    private static IEnumerable<Transaction> OrderTxsForEvaluationV3(
        IEnumerable<Transaction> txs,
        ImmutableArray<byte> preEvaluationHashBytes)
    {
        using SHA256 sha256 = SHA256.Create();

        // Some deterministic preordering is necessary.
        var groups = txs.GroupBy(tx => tx.Signer).OrderBy(group => group.Key).ToList();

        // Although strictly not necessary, additional hash computation removes zero padding
        // just in case.
        byte[] reHash = sha256.ComputeHash(preEvaluationHashBytes.ToArray());

        // As BigInteger uses little-endian, we take the last byte for parity to prevent
        // the value of reverse directly tied to the parity of startIndex below.
        bool reverse = reHash.Last() % 2 == 1;

        // This assumes the entropy of preEvaluationHash, thus reHash, is large enough and
        // its range with BigInteger conversion also is large enough that selection of
        // startIndex is approximately uniform.
        int startIndex = groups.Count <= 1
            ? 0
            : (int)(new BigInteger(reHash) % groups.Count);
        startIndex = startIndex >= 0 ? startIndex : -startIndex;

        var result = groups
            .Skip(startIndex)
            .Concat(groups.Take(startIndex));
        if (reverse)
        {
            result = result.Reverse();
        }

        return result.SelectMany(group => group.OrderBy(tx => tx.Nonce));
    }

    private IEnumerable<IAction> LoadActions(long index, Transaction tx)
    {
        foreach (var action in tx.Actions)
        {
            yield return ActionLoader.LoadAction(index, action);
        }
    }

    private int GetCapacity(RawBlock block)
    {
        var txCount = block.Transactions.Count;
        var actionCount = block.Transactions.Sum(tx => tx.Actions.Length);
        var blockActionCount = policyActionsRegistry.BeginBlockActions.Length
            + policyActionsRegistry.EndBlockActions.Length;
        var txActionCount = policyActionsRegistry.BeginTxActions.Length
            + policyActionsRegistry.EndTxActions.Length;
        var capacity = actionCount + blockActionCount + (txActionCount * txCount);
        return capacity;
    }
}
