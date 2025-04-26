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

    public IReadOnlyList<CommittedActionEvaluation> Evaluate(
        RawBlock block, HashDigest<SHA256> baseStateRootHash)
    {
        _logger.Information(
            "Evaluating actions in the block #{BlockHeight} " +
            "pre-evaluation hash {RawHash}...",
            block.Height,
            ByteUtil.Hex(block.RawHash.Bytes));
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        try
        {
            var world = stateStore.GetWorld(baseStateRootHash);
            var evaluations = ImmutableList<ActionEvaluation>.Empty;
            if (policyActionsRegistry.BeginBlockActions.Length > 0)
            {
                evaluations = evaluations.AddRange(EvaluatePolicyBeginBlockActions(block, world));
                world = evaluations.Last().OutputState;
            }

            evaluations = evaluations.AddRange([.. EvaluateBlock(block, world)]);

            if (policyActionsRegistry.EndBlockActions.Length > 0)
            {
                world = evaluations.Count > 0
                    ? evaluations.Last().OutputState
                    : world;
                evaluations = evaluations.AddRange(EvaluatePolicyEndBlockActions(block, world));
            }

            var committed = ToCommittedEvaluation(block, evaluations, baseStateRootHash);
            return committed;
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

    internal static IEnumerable<ActionEvaluation> EvaluateActions(
        RawBlock block,
        Transaction? tx,
        IWorld world,
        ImmutableArray<IAction> actions,
        IStateStore stateStore,
        bool isPolicyAction,
        ILogger? logger = null)
    {
        // IActionContext CreateActionContext(
        //     IWorld prevState,
        //     int randomSeed)
        // {
        //     return new ActionContext
        //     {
        //         Signer = tx?.Signer,
        //         TxId = tx?.Id ?? null,
        //         Miner = block.Miner,
        //         BlockHeight = block.Height,
        //         BlockProtocolVersion = block.ProtocolVersion,
        //         LastCommit = block.LastCommit,
        //         Txs = block.Transactions,
        //         PreviousState = prevState,
        //         IsPolicyAction = isPolicyAction,
        //         RandomSeed = randomSeed,
        //         MaxGasPrice = tx?.MaxGasPrice,
        //         Evidence = block.Evidence,
        //     };
        // }

        byte[] preEvaluationHashBytes = block.RawHash.Bytes.ToArray();
        var signature = tx?.Signature ?? [];
        var randomSeed = GenerateRandomSeed(preEvaluationHashBytes, signature, 0);

        IWorld state = world;
        foreach (var action in actions)
        {
            var actionContext = new ActionContext
            {
                Signer = tx?.Signer,
                TxId = tx?.Id ?? null,
                Miner = block.Miner,
                BlockHeight = block.Height,
                BlockProtocolVersion = block.ProtocolVersion,
                LastCommit = block.LastCommit,
                Txs = block.Transactions,
                PreviousState = state,
                IsPolicyAction = isPolicyAction,
                RandomSeed = randomSeed,
                MaxGasPrice = tx?.MaxGasPrice,
                Evidence = block.Evidence,
            };
            ActionEvaluation evaluation = EvaluateAction(
                block,
                tx,
                actionContext,
                action,
                stateStore,
                isPolicyAction,
                logger);

            yield return evaluation;

            state = evaluation.OutputState;

            unchecked
            {
                randomSeed++;
            }
        }
    }

    internal static ActionEvaluation EvaluateAction(
        RawBlock block,
        Transaction? tx,
        IActionContext context,
        IAction action,
        IStateStore stateStore,
        bool isPolicyAction,
        ILogger? logger = null)
    {
        if (!context.PreviousState.Trie.IsCommitted)
        {
            throw new InvalidOperationException(
                $"Given {nameof(context)} must have its previous state's " +
                $"{nameof(ITrie)} recorded.");
        }

        IActionContext inputContext = context;
        IWorld state = inputContext.PreviousState;
        Exception? exc = null;

        IActionContext CreateActionContext(IWorld newPrevState)
        {
            return new ActionContext
            {
                Signer = inputContext.Signer,
                TxId = inputContext.TxId,
                Miner = inputContext.Miner,
                BlockHeight = inputContext.BlockHeight,
                BlockProtocolVersion = inputContext.BlockProtocolVersion,
                LastCommit = inputContext.LastCommit,
                PreviousState = newPrevState,
                RandomSeed = inputContext.RandomSeed,
                IsPolicyAction = isPolicyAction,
                MaxGasPrice = tx?.MaxGasPrice,
                Txs = inputContext.Txs,
                Evidence = inputContext.Evidence,
            };
        }

        try
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            AccountMetrics.Initialize();
            context = CreateActionContext(state);
            state = action.Execute(context);
            logger?
                .ForContext("Tag", "Metric")
                .ForContext("Subtag", "ActionExecutionTime")
                .Information(
                    "Action {Action} took {DurationMs} ms to execute, " +
                    "GetState called {GetStateCount} times " +
                    "and took {GetStateDurationMs} ms",
                    action,
                    stopwatch.ElapsedMilliseconds,
                    AccountMetrics.GetStateCount.Value,
                    AccountMetrics.GetStateTimer.Value?.ElapsedMilliseconds);
        }
        catch (OutOfMemoryException e)
        {
            // Because OutOfMemory is thrown non-deterministically depending on the state
            // of the node, we should throw without further handling.
            var message =
                "Action {Action} of tx {TxId} of block #{BlockHeight} with " +
                "pre-evaluation hash {RawHash} threw an exception " +
                "during execution";
            logger?.Error(
                e,
                message,
                action,
                tx?.Id,
                block.Height,
                ByteUtil.Hex(block.RawHash.Bytes));
            throw;
        }
        catch (Exception e)
        {
            var message =
                "Action {Action} of tx {TxId} of block #{BlockHeight} with " +
                "pre-evaluation hash {RawHash} threw an exception " +
                "during execution";
            logger?.Error(
                e,
                message,
                action,
                tx?.Id,
                block.Height,
                ByteUtil.Hex(block.RawHash.Bytes));
            var innerMessage =
                $"The action {action} (block #{block.Height}, " +
                $"pre-evaluation hash " +
                $"{ByteUtil.Hex(block.RawHash.Bytes)}, " +
                $"tx {tx?.Id} threw an exception during execution.  " +
                "See also this exception's InnerException property";
            logger?.Error(
                "{Message}\nInnerException: {ExcMessage}", innerMessage, e.Message);
            exc = new UnexpectedlyTerminatedActionException(
                innerMessage,
                block.RawHash,
                block.Height,
                tx?.Id,
                null,
                action,
                e);
        }

        state = stateStore.CommitWorld(state);

        if (!state.Trie.IsCommitted)
        {
            throw new InvalidOperationException(
                $"Failed to record {nameof(IAccount)}'s {nameof(ITrie)}.");
        }

        return new ActionEvaluation
        {
            Action = action,
            InputContext = inputContext,
            OutputState = state,
            Exception = exc,
        };
    }

    internal static IEnumerable<Transaction> OrderTxsForEvaluation(
        int protocolVersion,
        IEnumerable<Transaction> txs,
        ImmutableArray<byte> preEvaluationHashBytes)
    {
        return OrderTxsForEvaluationV3(txs, preEvaluationHashBytes);
    }

    internal IEnumerable<ActionEvaluation> EvaluateBlock(
        RawBlock block,
        IWorld previousState)
    {
        IWorld delta = previousState;
        IEnumerable<Transaction> orderedTxs = OrderTxsForEvaluation(
            block.ProtocolVersion,
            block.Transactions,
            block.RawHash.Bytes);

        foreach (Transaction tx in orderedTxs)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            IEnumerable<ActionEvaluation> evaluations = EvaluateTx(
                block: block,
                tx: tx,
                previousState: delta);

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

    internal IEnumerable<ActionEvaluation> EvaluateTx(
        RawBlock block,
        Transaction tx,
        IWorld previousState)
    {
        GasTracer.Initialize(tx.GasLimit ?? long.MaxValue);
        var evaluations = ImmutableList<ActionEvaluation>.Empty;
        if (policyActionsRegistry.BeginTxActions.Length > 0)
        {
            GasTracer.IsTxAction = true;
            evaluations = evaluations.AddRange(
                EvaluatePolicyBeginTxActions(block, tx, previousState));
            previousState = evaluations.Last().OutputState;
            GasTracer.IsTxAction = false;
        }

        ImmutableList<IAction> actions =
            ImmutableList.CreateRange(LoadActions(block.Height, tx));
        evaluations = evaluations.AddRange(EvaluateActions(
            block: block,
            tx: tx,
            world: previousState,
            actions: actions,
            stateStore: stateStore,
            isPolicyAction: false,
            logger: _logger));

        if (policyActionsRegistry.EndTxActions.Length > 0)
        {
            previousState = evaluations.Count > 0
                ? evaluations.Last().OutputState
                : previousState;
            evaluations = evaluations.AddRange(
                EvaluatePolicyEndTxActions(block, tx, previousState));
        }

        GasTracer.Release();

        return evaluations;
    }

    internal ActionEvaluation[] EvaluatePolicyBeginBlockActions(
        RawBlock block, IWorld previousState)
    {
        _logger.Information(
            "Evaluating policy begin block actions for block #{BlockHeight} {BlockHash}",
            block.Height,
            ByteUtil.Hex(block.RawHash.Bytes));

        return EvaluateActions(
            block: block,
            tx: null,
            world: previousState,
            actions: policyActionsRegistry.BeginBlockActions,
            stateStore: stateStore,
            isPolicyAction: true,
            logger: _logger).ToArray();
    }

    internal ActionEvaluation[] EvaluatePolicyEndBlockActions(
        RawBlock block,
        IWorld previousState)
    {
        _logger.Information(
            $"Evaluating policy end block actions for block #{block.Height} " +
            $"{ByteUtil.Hex(block.RawHash.Bytes)}");

        return EvaluateActions(
            block: block,
            tx: null,
            world: previousState,
            actions: policyActionsRegistry.EndBlockActions,
            stateStore: stateStore,
            isPolicyAction: true,
            logger: _logger).ToArray();
    }

    internal ActionEvaluation[] EvaluatePolicyBeginTxActions(
        RawBlock block,
        Transaction transaction,
        IWorld previousState)
    {
        _logger.Information(
            $"Evaluating policy begin tx actions for block #{block.Height} " +
            $"{ByteUtil.Hex(block.RawHash.Bytes)}");

        return EvaluateActions(
            block: block,
            tx: transaction,
            world: previousState,
            actions: policyActionsRegistry.BeginTxActions,
            stateStore: stateStore,
            isPolicyAction: true,
            logger: _logger).ToArray();
    }

    internal ActionEvaluation[] EvaluatePolicyEndTxActions(
        RawBlock block,
        Transaction transaction,
        IWorld previousState)
    {
        _logger.Information(
            $"Evaluating policy end tx actions for block #{block.Height} " +
            $"{ByteUtil.Hex(block.RawHash.Bytes)}");

        return EvaluateActions(
            block: block,
            tx: transaction,
            world: previousState,
            actions: policyActionsRegistry.EndTxActions,
            stateStore: stateStore,
            isPolicyAction: true,
            logger: _logger).ToArray();
    }

    internal IReadOnlyList<CommittedActionEvaluation>
        ToCommittedEvaluation(
            RawBlock block,
            IReadOnlyList<ActionEvaluation> evaluations,
            HashDigest<SHA256>? baseStateRootHash)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        var committedEvaluations = new List<CommittedActionEvaluation>();
        foreach (var evaluation in evaluations)
        {
#pragma warning disable SA1118
            var committedEvaluation = new CommittedActionEvaluation
            {
                Action = evaluation.Action,
                InputContext = new CommittedActionContext
                {
                    Signer = evaluation.InputContext.Signer,
                    TxId = evaluation.InputContext.TxId,
                    Miner = evaluation.InputContext.Miner,
                    BlockHeight = evaluation.InputContext.BlockHeight,
                    BlockProtocolVersion = evaluation.InputContext.BlockProtocolVersion,
                    PreviousState = evaluation.InputContext.PreviousState.Trie.IsCommitted
                        ? evaluation.InputContext.PreviousState.Trie.Hash
                        : throw new ArgumentException("Trie is not recorded"),
                    RandomSeed = evaluation.InputContext.RandomSeed,
                    IsPolicyAction = evaluation.InputContext.IsPolicyAction,
                },
                OutputState = evaluation.OutputState.Trie.IsCommitted
                    ? evaluation.OutputState.Trie.Hash
                    : throw new ArgumentException("Trie is not recorded"),
                Exception = evaluation.Exception,
            };
            committedEvaluations.Add(committedEvaluation);
#pragma warning restore SA1118
        }

        return committedEvaluations;
    }

    private static IEnumerable<Transaction> OrderTxsForEvaluationV0(
        IEnumerable<Transaction> txs,
        ImmutableArray<byte> preEvaluationHashBytes)
    {
        // As the order of transactions should be unpredictable until a block is mined,
        // the sorter key should be derived from both a block hash and a txid.
        var maskInteger = new BigInteger(preEvaluationHashBytes.ToArray());

        // Transactions with the same signers are grouped first and the ordering of the groups
        // is determined by the XOR aggregate of the txid's in the group with XOR bitmask
        // applied using the pre-evaluation hash provided.  Then within each group,
        // transactions are ordered by nonce.
        return txs
            .GroupBy(tx => tx.Signer)
            .OrderBy(
                group => maskInteger ^ group
                    .Select(tx => new BigInteger(tx.Id.Bytes.ToArray()))
                    .Aggregate((first, second) => first ^ second))
            .SelectMany(group => group.OrderBy(tx => tx.Nonce));
    }

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
        if (tx.Actions is { } actions)
        {
            foreach (IValue rawAction in actions)
            {
                yield return ActionLoader.LoadAction(index, rawAction);
            }
        }
    }
}
