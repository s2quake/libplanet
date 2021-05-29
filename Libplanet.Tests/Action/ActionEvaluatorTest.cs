using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Tests.Common.Action;
using Libplanet.Tests.Fixtures;
using Libplanet.Tests.Store;
using Libplanet.Tests.Store.Trie;
using Libplanet.Tests.Tx;
using Libplanet.Tx;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Action
{
    public class ActionEvaluatorTest
    {
        private static readonly AccountStateGetter _nullAccountStateGetter = address => null;
        private static readonly AccountBalanceGetter _nullAccountBalanceGetter =
            (address, currency) => new FungibleAssetValue(currency);

        private readonly ILogger _logger;
        private readonly BlockPolicy<DumbAction> _policy;
        private readonly StoreFixture _storeFx;
        private readonly TxFixture _txFx;

        public ActionEvaluatorTest(ITestOutputHelper output)
        {
            Log.Logger = _logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithThreadId()
                .WriteTo.TestOutput(output)
                .CreateLogger()
                .ForContext<ActionEvaluatorTest>();

            _policy = new BlockPolicy<DumbAction>(new MinerReward(1), maxBlockBytes: 50 * 1024);
            _storeFx = new DefaultStoreFixture(memory: true, blockAction: _policy.BlockAction);
            _txFx = new TxFixture(null);
        }

        [Fact]
        public void Idempotent()
        {
            // NOTE: This test checks that blocks can be evaluated idempotently. Also it checks
            // the action results in pre-evaluation step and in evaluation step are equal.
            const int repeatCount = 2;
            var signer = new PrivateKey();
            var timestamp = DateTimeOffset.UtcNow;
            var txAddress = signer.ToAddress();
            var txs = new[]
            {
                Transaction<RandomAction>.Create(
                    nonce: 0,
                    privateKey: signer,
                    genesisHash: null,
                    actions: new[] { new RandomAction(txAddress), }),
            };
            var stateStore =
                new TrieStateStore(new MemoryKeyValueStore(), new MemoryKeyValueStore());
            var noStateRootBlock = TestUtils.MineGenesis(
                timestamp: timestamp,
                transactions: txs);
            var stateRootBlock = TestUtils.MineGenesis(
                timestamp: timestamp,
                transactions: txs).AttachStateRootHash(stateStore, null);
            StateGetter<RandomAction> nullStateGetter =
                (address, hashDigest, stateCompleter) => null;
            BalanceGetter<RandomAction> nullBalanceGetter =
                (address, currency, hashDigest, fungibleAssetStateCompleter)
                    => new FungibleAssetValue(currency);
            var actionEvaluator =
                new ActionEvaluator<RandomAction>(
                    policyBlockAction: null,
                    stateGetter: nullStateGetter,
                    balanceGetter: nullBalanceGetter,
                    trieGetter: null);
            var generatedRandomNumbers = new List<int>();

            Assert.NotEqual(stateRootBlock.Hash, noStateRootBlock.Hash);
            Assert.Equal(stateRootBlock.PreEvaluationHash, noStateRootBlock.PreEvaluationHash);

            for (int i = 0; i < repeatCount; ++i)
            {
                var actionEvaluations = actionEvaluator.Evaluate(
                    noStateRootBlock,
                    StateCompleterSet<RandomAction>.Reject);
                generatedRandomNumbers.Add(
                    (Integer)actionEvaluations[0].OutputStates.GetState(txAddress));
                actionEvaluations = actionEvaluator.Evaluate(
                    stateRootBlock,
                    StateCompleterSet<RandomAction>.Reject);
                generatedRandomNumbers.Add(
                    (Integer)actionEvaluations[0].OutputStates.GetState(txAddress));
            }

            for (int i = 1; i < generatedRandomNumbers.Count; ++i)
            {
                Assert.Equal(generatedRandomNumbers[0], generatedRandomNumbers[i]);
            }
        }

        [Fact]
        public async void Evaluate()
        {
            var privateKey = new PrivateKey();
            var address = privateKey.ToAddress();
            long blockIndex = 1;

            TestEvaluateAction action = new TestEvaluateAction();

            var store = new DefaultStore(null);
            var stateStore =
                new TrieStateStore(new MemoryKeyValueStore(), new MemoryKeyValueStore());
            var chain = TestUtils.MakeBlockChain<TestEvaluateAction>(
                policy: new BlockPolicy<TestEvaluateAction>(),
                store: store,
                stateStore: stateStore);
            var tx = Transaction<TestEvaluateAction>.Create(
                nonce: 0,
                privateKey: privateKey,
                genesisHash: chain.Genesis.Hash,
                actions: new[] { action });

            chain.StageTransaction(tx);
            await chain.MineBlock(_storeFx.Address1);

            var actionEvaluation = chain.ActionEvaluator.Evaluate(
                chain.Tip,
                StateCompleterSet<TestEvaluateAction>.Recalculate);
            Assert.False(actionEvaluation[0].InputContext.BlockAction);

            Assert.Equal(
                chain.GetState(TestEvaluateAction.SignerKey),
                (Text)address.ToHex());
            Assert.Equal(
                chain.GetState(TestEvaluateAction.MinerKey),
                (Text)_storeFx.Address1.ToHex());
            var state = chain.GetState(TestEvaluateAction.BlockIndexKey);
            Assert.Equal((long)(Integer)state, blockIndex);
        }

        [SuppressMessage(
            "Microsoft.StyleCop.CSharp.ReadabilityRules",
            "SA1118",
            Justification = "Long array literals should be multiline.")]
        [Fact]
        public void EvaluateTxsGradually()
        {
            DumbAction MakeAction(Address address, char identifier, Address? transferTo = null)
            {
                return new DumbAction(
                    targetAddress: address,
                    item: identifier.ToString(),
                    recordRehearsal: false,
                    recordRandom: true,
                    transfer: transferTo is Address to
                        ? Tuple.Create<Address, Address, BigInteger>(address, to, 5)
                        : null);
            }

            Address[] addresses =
            {
                _txFx.Address1,
                _txFx.Address2,
                _txFx.Address3,
                _txFx.Address4,
                _txFx.Address5,
            };
            Block<DumbAction> genesis = MineGenesis<DumbAction>();
            StateGetter<DumbAction> nullStateGetter =
                (address, hashDigest, stateCompleter) => null;
            BalanceGetter<DumbAction> nullBalanceGetter =
                (address, currency, hashDigest, fungibleAssetStateCompleter)
                    => new FungibleAssetValue(currency);
            ActionEvaluator<DumbAction> actionEvaluator = new ActionEvaluator<DumbAction>(
                policyBlockAction: null,
                stateGetter: nullStateGetter,
                balanceGetter: nullBalanceGetter,
                trieGetter: null);
            IAccountStateDelta previousStates = genesis.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    _nullAccountStateGetter,
                    _nullAccountBalanceGetter,
                    genesis.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    _nullAccountStateGetter,
                    _nullAccountBalanceGetter,
                    genesis.Miner.GetValueOrDefault());
            Assert.Empty(
                actionEvaluator.EvaluateTxsGradually(
                    block: genesis,
                    previousStates: previousStates));

            Transaction<DumbAction>[] block1Txs =
            {
                Transaction<DumbAction>.Create(
                    nonce: 0,
                    privateKey: _txFx.PrivateKey1,
                    genesisHash: genesis.Hash,
                    actions: new[]
                    {
                        MakeAction(addresses[0], 'A', addresses[1]),
                        MakeAction(addresses[1], 'B', addresses[2]),
                    },
                    timestamp: DateTimeOffset.MinValue.AddSeconds(1)),
                Transaction<DumbAction>.Create(
                    nonce: 0,
                    privateKey: _txFx.PrivateKey2,
                    genesisHash: genesis.Hash,
                    actions: new[] { MakeAction(addresses[2], 'C', addresses[3]) },
                    timestamp: DateTimeOffset.MinValue.AddSeconds(2)),
                Transaction<DumbAction>.Create(
                    nonce: 0,
                    privateKey: _txFx.PrivateKey3,
                    genesisHash: genesis.Hash,
                    actions: new DumbAction[0],
                    timestamp: DateTimeOffset.MinValue.AddSeconds(8)),
            };
            int i = 0;
            foreach (Transaction<DumbAction> tx in block1Txs)
            {
                _logger.Debug("{0}[{1}] = {2}", nameof(block1Txs), i, tx.Id);
            }

            Block<DumbAction> block1 = MineNext(genesis, block1Txs, new byte[] { });
            previousStates = block1.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    _nullAccountStateGetter,
                    _nullAccountBalanceGetter,
                    block1.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    _nullAccountStateGetter,
                    _nullAccountBalanceGetter,
                    block1.Miner.GetValueOrDefault());
            var pairs = actionEvaluator.EvaluateTxsGradually(
                block1,
                previousStates).ToImmutableArray();
            int randomValue = 0;
            (int, int, string[], Address)[] expectations =
            {
                (1, 0, new[] { null, null, "C", null, null }, _txFx.Address2),
                (0, 0, new[] { "A", null, "C", null, null }, _txFx.Address1),
                (0, 1, new[] { "A", "B", "C", null, null }, _txFx.Address1),
            };
            Assert.Equal(expectations.Length, pairs.Length);
            foreach (var (expect, pair) in expectations.Zip(pairs, ValueTuple.Create))
            {
                ActionEvaluation eval = pair.Item2;
                Assert.Equal(block1Txs[expect.Item1], pair.Item1);
                Assert.Equal(block1Txs[expect.Item1].Actions[expect.Item2], eval.Action);
                Assert.Equal(expect.Item4, eval.InputContext.Signer);
                Assert.Equal(GenesisMinerAddress, eval.InputContext.Miner);
                Assert.Equal(block1.Index, eval.InputContext.BlockIndex);
                Assert.False(eval.InputContext.Rehearsal);
                randomValue = eval.InputContext.Random.Next();
                Assert.Equal(
                    (Integer)eval.OutputStates.GetState(
                        DumbAction.RandomRecordsAddress),
                    (Integer)randomValue);
                Assert.Equal(
                    expect.Item3,
                    addresses.Select(eval.OutputStates.GetState)
                        .Select(x => x is Text t ? t.Value : null));
            }

            previousStates = block1.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    _nullAccountStateGetter,
                    _nullAccountBalanceGetter,
                    block1.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    _nullAccountStateGetter,
                    _nullAccountBalanceGetter,
                    block1.Miner.GetValueOrDefault());
            ActionEvaluation[] evals1 = actionEvaluator.EvaluateBlock(
                block1,
                DateTimeOffset.UtcNow,
                previousStates).ToArray();
            IImmutableDictionary<Address, IValue> dirty1 = evals1.GetDirtyStates();
            IImmutableDictionary<(Address, Currency), FungibleAssetValue> balances1 =
                evals1.GetDirtyBalances();
            Assert.Equal(
                new Dictionary<Address, IValue>
                {
                    [addresses[0]] = (Text)"A",
                    [addresses[1]] = (Text)"B",
                    [addresses[2]] = (Text)"C",
                    [DumbAction.RandomRecordsAddress] = (Integer)randomValue,
                }.ToImmutableDictionary(),
                dirty1);
            Assert.Equal(
                new Dictionary<(Address, Currency), FungibleAssetValue>
                {
                    [(addresses[0], DumbAction.DumbCurrency)] =
                        new FungibleAssetValue(DumbAction.DumbCurrency, -5, 0),
                    [(addresses[1], DumbAction.DumbCurrency)] =
                        new FungibleAssetValue(DumbAction.DumbCurrency),
                    [(addresses[2], DumbAction.DumbCurrency)] =
                        new FungibleAssetValue(DumbAction.DumbCurrency),
                    [(addresses[3], DumbAction.DumbCurrency)] =
                        new FungibleAssetValue(DumbAction.DumbCurrency, 5, 0),
                }.ToImmutableDictionary(),
                balances1);

            Transaction<DumbAction>[] block2Txs =
            {
                // Note that these timestamps in themselves does not have any meanings but are
                // only arbitrary.  These purpose to make their evaluation order in a block
                // equal to the order we (the test) intend:
                Transaction<DumbAction>.Create(
                    0,
                    _txFx.PrivateKey1,
                    genesis.Hash,
                    new[] { MakeAction(addresses[0], 'D') },
                    timestamp: DateTimeOffset.MinValue.AddSeconds(1)),
                Transaction<DumbAction>.Create(
                    0,
                    _txFx.PrivateKey2,
                    genesis.Hash,
                    new[] { MakeAction(addresses[3], 'E') },
                    timestamp: DateTimeOffset.MinValue.AddSeconds(3)),
                Transaction<DumbAction>.Create(
                    0,
                    _txFx.PrivateKey3,
                    genesis.Hash,
                    new[]
                    {
                        new DumbAction(
                            addresses[4],
                            "RecordRehearsal",
                            transferFrom: addresses[0],
                            transferTo: addresses[4],
                            transferAmount: 8,
                            recordRehearsal: true,
                            recordRandom: true),
                    },
                    timestamp: DateTimeOffset.MinValue.AddSeconds(5)),
            };
            i = 0;
            foreach (Transaction<DumbAction> tx in block2Txs)
            {
                _logger.Debug("{0}[{1}] = {2}", nameof(block2Txs), i, tx.Id);
            }

            Block<DumbAction> block2 = MineNext(block1, block2Txs, new byte[] { });
            AccountStateGetter accountStateGetter = dirty1.GetValueOrDefault;
            AccountBalanceGetter accountBalanceGetter = (address, currency)
                => balances1.TryGetValue((address, currency), out FungibleAssetValue v)
                    ? v
                    : new FungibleAssetValue(currency);
            previousStates = block2.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    accountStateGetter,
                    accountBalanceGetter,
                    block2.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    accountStateGetter,
                    accountBalanceGetter,
                    block2.Miner.GetValueOrDefault());
            pairs = actionEvaluator.EvaluateTxsGradually(
                block2,
                previousStates).ToImmutableArray();
            expectations = new[]
            {
                (0, 0, new[] { "A,D", "B", "C", null, null }, _txFx.Address1),
                (1, 0, new[] { "A,D", "B", "C", "E", null }, _txFx.Address2),
                (
                    2,
                    0,
                    new[] { "A,D", "B", "C", "E", "RecordRehearsal:False" },
                    _txFx.Address3
                ),
            };
            Assert.Equal(expectations.Length, pairs.Length);
            foreach (var (expect, pair) in expectations.Zip(pairs, ValueTuple.Create))
            {
                ActionEvaluation eval = pair.Item2;
                Assert.Equal(block2Txs[expect.Item1], pair.Item1);
                Assert.Equal(
                    block2Txs[expect.Item1].Actions[expect.Item2],
                    eval.Action);
                Assert.Equal(expect.Item4, eval.InputContext.Signer);
                Assert.Equal(GenesisMinerAddress, eval.InputContext.Miner);
                Assert.Equal(block2.Index, eval.InputContext.BlockIndex);
                Assert.False(eval.InputContext.Rehearsal);
                Assert.Null(eval.Exception);
                Assert.Equal(
                    expect.Item3,
                    addresses
                        .Select(eval.OutputStates.GetState)
                        .Select(x => x is Text t ? t.Value : null));
                randomValue = eval.InputContext.Random.Next();
                Assert.Equal(
                    eval.OutputStates.GetState(
                        DumbAction.RandomRecordsAddress),
                    (Integer)randomValue);
            }

            accountStateGetter = dirty1.GetValueOrDefault;
            accountBalanceGetter = (address, currency) => balances1.TryGetValue(
                (address, currency), out FungibleAssetValue value)
                    ? value
                    : new FungibleAssetValue(currency);
            previousStates = block2.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    accountStateGetter,
                    accountBalanceGetter,
                    block2.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    accountStateGetter,
                    accountBalanceGetter,
                    block2.Miner.GetValueOrDefault());
            var evals2 = actionEvaluator.EvaluateBlock(
                block2,
                DateTimeOffset.UtcNow,
                previousStates).ToArray();
            IImmutableDictionary<Address, IValue> dirty2 = evals2.GetDirtyStates();
            IImmutableDictionary<(Address, Currency), FungibleAssetValue> balances2 =
                evals2.GetDirtyBalances();
            Assert.Equal(
                new Dictionary<Address, IValue>
                {
                    [addresses[0]] = (Text)"A,D",
                    [addresses[3]] = (Text)"E",
                    [addresses[4]] = (Text)"RecordRehearsal:False",
                    [DumbAction.RandomRecordsAddress] = (Integer)randomValue,
                }.ToImmutableDictionary(),
                dirty2);
        }

        [Fact]
        public void EvaluateTxGradually()
        {
            Address[] addresses =
            {
                new PrivateKey().ToAddress(),
                new PrivateKey().ToAddress(),
                new PrivateKey().ToAddress(),
            };
            DumbAction[] actions =
            {
                new DumbAction(
                    targetAddress: addresses[0],
                    item: "0",
                    transferFrom: addresses[0],
                    transferTo: addresses[1],
                    transferAmount: 5,
                    recordRandom: true),
                new DumbAction(
                    targetAddress: addresses[1],
                    item: "1",
                    transferFrom: addresses[2],
                    transferTo: addresses[1],
                    transferAmount: 10,
                    recordRandom: true),
                new DumbAction(
                    targetAddress: addresses[0],
                    item: "2",
                    transferFrom: addresses[1],
                    transferTo: addresses[0],
                    transferAmount: 10,
                    recordRandom: true),
                new DumbAction(addresses[2], "R", true, recordRandom: true),
            };
            var tx =
                Transaction<DumbAction>.Create(0, _txFx.PrivateKey1, null, actions);
            var block = new Block<DumbAction>(
                index: 1,
                difficulty: 0,
                totalDifficulty: 0,
                nonce: new Nonce(new byte[0]),
                miner: addresses[0],
                previousHash: null,
                timestamp: DateTimeOffset.UtcNow,
                transactions: ImmutableArray.Create(tx));
            StateGetter<DumbAction> nullStateGetter =
                (address, hashDigest, stateCompleter) => null;
            BalanceGetter<DumbAction> nullBalanceGetter =
                (address, currency, hashDigest, fungibleAssetStateCompleter)
                    => new FungibleAssetValue(currency);
            var actionEvaluator = new ActionEvaluator<DumbAction>(
                policyBlockAction: null,
                stateGetter: nullStateGetter,
                balanceGetter: nullBalanceGetter,
                trieGetter: null);

            foreach (bool rehearsal in new[] { false, true })
            {
                DumbAction.RehearsalRecords.Value =
                    ImmutableList<(Address, string)>.Empty;
                var evaluations = actionEvaluator.EvaluateTxGradually(
                    block: block,
                    tx: tx,
                    previousStates: new AccountStateDeltaImpl(
                        _nullAccountStateGetter,
                        _nullAccountBalanceGetter,
                        tx.Signer),
                    rehearsal: rehearsal).ToImmutableArray();

                Assert.Equal(actions.Length, evaluations.Length);
                string[][] expectedStates =
                {
                    new[] { "0", null, null },
                    new[] { "0", "1", null },
                    new[] { "0,2", "1", null },
                    new[] { "0,2", "1", $"R:{rehearsal}" },
                };
                BigInteger[][] expectedBalances =
                {
                    new BigInteger[] { -5, 5, 0 },
                    new BigInteger[] { -5, 15, -10 },
                    new BigInteger[] { 5, 5, -10 },
                    new BigInteger[] { 5, 5, -10 },
                };

                Currency currency = DumbAction.DumbCurrency;
                IValue[] initStates = new IValue[3];
                BigInteger[] initBalances = new BigInteger[3];
                for (int i = 0; i < evaluations.Length; i++)
                {
                    ActionEvaluation eval = evaluations[i];
                    Assert.Equal(actions[i], eval.Action);
                    Assert.Equal(_txFx.Address1, eval.InputContext.Signer);
                    Assert.Equal(tx.Id, eval.InputContext.TxId);
                    Assert.Equal(addresses[0], eval.InputContext.Miner);
                    Assert.Equal(1, eval.InputContext.BlockIndex);
                    Assert.Equal(rehearsal, eval.InputContext.Rehearsal);
                    Assert.Equal(
                        (Integer)eval.OutputStates.GetState(
                            DumbAction.RandomRecordsAddress),
                        (Integer)eval.InputContext.Random.Next());
                    ActionEvaluation prevEval = i > 0 ? evaluations[i - 1] : null;
                    Assert.Equal(
                        prevEval is null
                            ? initStates
                            : addresses.Select(prevEval.OutputStates.GetState),
                        addresses.Select(eval.InputContext.PreviousStates.GetState));
                    Assert.Equal(
                        expectedStates[i],
                        addresses.Select(eval.OutputStates.GetState)
                            .Select(x => x is Text t ? t.Value : null));
                    Assert.Equal(
                        prevEval is null
                            ? initBalances
                            : addresses.Select(a =>
                                prevEval.OutputStates.GetBalance(a, currency).RawValue),
                        addresses.Select(
                            a => eval.InputContext.PreviousStates
                                    .GetBalance(a, currency).RawValue));
                    Assert.Equal(
                        expectedBalances[i],
                        addresses.Select(a => eval.OutputStates.GetBalance(a, currency).RawValue));
                }

                if (rehearsal)
                {
                    Assert.Contains(
                        (addresses[2], "R"),
                        DumbAction.RehearsalRecords.Value);
                }
                else
                {
                    Assert.DoesNotContain(
                        (addresses[2], "R"),
                        DumbAction.RehearsalRecords.Value);
                }

                DumbAction.RehearsalRecords.Value =
                    ImmutableList<(Address, string)>.Empty;
                IAccountStateDelta delta = actionEvaluator.EvaluateTxResult(
                    block: block,
                    tx: tx,
                    previousStates: new AccountStateDeltaImpl(
                        _nullAccountStateGetter,
                        _nullAccountBalanceGetter,
                        tx.Signer),
                    rehearsal: rehearsal);
                Assert.Equal(
                    evaluations[3].OutputStates.GetUpdatedStates(),
                    delta.GetUpdatedStates());

                if (rehearsal)
                {
                    Assert.Contains(
                        (addresses[2], "R"),
                        DumbAction.RehearsalRecords.Value);
                }
                else
                {
                    Assert.DoesNotContain(
                        (addresses[2], "R"),
                        DumbAction.RehearsalRecords.Value);
                }
            }
        }

        [Fact]
        public void EvaluateBlockWithInvalidTxUpdatedAddresses()
        {
            ImmutableArray<IValue> rawActions =
                _txFx.TxWithActions
                    .ToRawTransaction(false).Actions.ToImmutableArray();
            Block<PolymorphicAction<BaseAction>> genesis =
                TestUtils.MineGenesis<PolymorphicAction<BaseAction>>();
            RawTransaction rawTxWithoutSig = new RawTransaction(
                0,
                _txFx.Address1.ByteArray,
                genesis.Hash.ByteArray,
                ImmutableArray<ImmutableArray<byte>>.Empty,
                _txFx.PublicKey1.Format(false).ToImmutableArray(),
                DateTimeOffset.UtcNow.ToString(
                    "yyyy-MM-ddTHH:mm:ss.ffffffZ",
                    CultureInfo.InvariantCulture
                ),
                rawActions,
                ImmutableArray<byte>.Empty
            );
            byte[] sig = _txFx.PrivateKey1.Sign(
                new Transaction<PolymorphicAction<BaseAction>>(
                    rawTxWithoutSig
                ).Serialize(false)
            );
            var invalidTx = new Transaction<PolymorphicAction<BaseAction>>(
                new RawTransaction(
                    0,
                    rawTxWithoutSig.Signer,
                    rawTxWithoutSig.GenesisHash,
                    rawTxWithoutSig.UpdatedAddresses,
                    rawTxWithoutSig.PublicKey,
                    rawTxWithoutSig.Timestamp,
                    rawTxWithoutSig.Actions,
                    sig.ToImmutableArray()
                )
            );
            Block<PolymorphicAction<BaseAction>> invalidBlock = TestUtils.MineNext(
                previousBlock: genesis,
                txs: new List<Transaction<PolymorphicAction<BaseAction>>> { invalidTx });

            StateGetter<PolymorphicAction<BaseAction>> nullStateGetter =
                (address, hashDigest, stateCompleter) => null;
            BalanceGetter<PolymorphicAction<BaseAction>> nullBalanceGetter =
                (address, currency, hashDigest, fungibleAssetStateCompleter)
                    => new FungibleAssetValue(currency);
            var actionEvaluator = new ActionEvaluator<PolymorphicAction<BaseAction>>(
                policyBlockAction: null,
                stateGetter: nullStateGetter,
                balanceGetter: nullBalanceGetter,
                trieGetter: null);
            AccountStateGetter nullAccountStateGetter = (address) => null;
            AccountBalanceGetter nullAccountBalanceGetter =
                (address, currency) => new FungibleAssetValue(currency);
            IAccountStateDelta previousStates = invalidBlock.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    nullAccountStateGetter,
                    nullAccountBalanceGetter,
                    invalidBlock.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    nullAccountStateGetter,
                    nullAccountBalanceGetter,
                    invalidBlock.Miner.GetValueOrDefault());
            Assert.Throws<InvalidTxUpdatedAddressesException>(() =>
                actionEvaluator.EvaluateBlock(
                    invalidBlock,
                    DateTimeOffset.UtcNow,
                    previousStates).ToList());
        }

        [Fact]
        public void EvaluateTxResultThrowingException()
        {
            var action = new ThrowException { ThrowOnRehearsal = false, ThrowOnExecution = true };
            Transaction<ThrowException> tx = Transaction<ThrowException>.Create(
                0,
                _txFx.PrivateKey1,
                null,
                new[] { action },
                ImmutableHashSet<Address>.Empty,
                DateTimeOffset.UtcNow);
            var hash = new BlockHash(GetRandomBytes(32));
            StateGetter<ThrowException> nullStateGetter =
                (address, hashDigest, stateCompleter) => null;
            BalanceGetter<ThrowException> nullBalanceGetter =
                (address, currency, hashDigest, fungibleAssetStateCompleter)
                    => new FungibleAssetValue(currency);
            var actionEvaluator = new ActionEvaluator<ThrowException>(
                policyBlockAction: null,
                stateGetter: nullStateGetter,
                balanceGetter: nullBalanceGetter,
                trieGetter: null);
            var block = new Block<ThrowException>(
                index: 123,
                difficulty: 0,
                totalDifficulty: 0,
                nonce: new Nonce(new byte[0]),
                miner: GenesisMinerAddress,
                previousHash: null,
                timestamp: DateTimeOffset.UtcNow,
                transactions: ImmutableArray.Create(tx));
            var nextStates = actionEvaluator.EvaluateTxResult(
                block: block,
                tx: tx,
                previousStates: new AccountStateDeltaImpl(
                    _nullAccountStateGetter,
                    _nullAccountBalanceGetter,
                    tx.Signer),
                rehearsal: false);

            Assert.Empty(nextStates.GetUpdatedStates());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EvaluateGradually(bool rehearsal)
        {
            var fx = new IntegerSet(new[] { 5, 10 });

            // a: ((5 + 1) * 2) + 3 = 15
            (Transaction<Arithmetic> a, var deltaA) = fx.Sign(
                0,
                Arithmetic.Add(1),
                Arithmetic.Mul(2),
                Arithmetic.Add(3)
            );

            Block<Arithmetic> blockA = await fx.Mine();
            ActionEvaluation[] evalsA = ActionEvaluator<DumbAction>.EvaluateGradually(
                blockA.Hash,
                blockIndex: blockA.Index,
                txid: a.Id,
                previousStates: fx.CreateAccountStateDelta(0, blockA.PreviousHash),
                miner: blockA.Miner ?? default,
                signer: a.Signer,
                signature: a.Signature,
                actions: a.Actions.ToImmutableArray<IAction>(),
                rehearsal: rehearsal,
                previousBlockStatesTrie: fx.GetTrie(blockA.PreviousHash),
                blockAction: false).ToArray();

            Assert.Equal(evalsA.Length, deltaA.Count - 1);
            for (int i = 0; i < evalsA.Length; i++)
            {
                ActionEvaluation eval = evalsA[i];
                _logger.Debug("evalsA[{0}] = {1}", i, eval);
                _logger.Debug("a.Actions[{0}] = {1}", i, a.Actions[i]);
                Assert.Equal(a.Actions[i], eval.Action);
                IActionContext context = eval.InputContext;
                Assert.Equal(a.Id, context.TxId);
                Assert.Equal(blockA.Miner, context.Miner);
                Assert.Equal(blockA.Index, context.BlockIndex);
                Assert.Equal(
                    deltaA[i].RootHash,
                    context.PreviousStateRootHash);
                Assert.Equal(a.Signer, context.Signer);
                Assert.False(context.BlockAction);
                Assert.Equal(rehearsal, context.Rehearsal);
                IAccountStateDelta prevStates = context.PreviousStates;
                Assert.Equal(
                    i > 0 ? new[] { a.Signer } : new Address[0],
                    prevStates.UpdatedAddresses);
                Assert.Equal((Integer)deltaA[i].Value, prevStates.GetState(a.Signer));
                IAccountStateDelta outputStates = eval.OutputStates;
                Assert.Equal(new[] { a.Signer }, outputStates.UpdatedAddresses);
                Assert.Equal((Integer)deltaA[i + 1].Value, outputStates.GetState(a.Signer));
                Assert.Null(eval.Exception);
            }

            // b: error(10 - 3) + -3 =
            //         (10 - 3)      = 7  (only input of error() is left)
            (Transaction<Arithmetic> b, var deltaB) = fx.Sign(
                1,
                Arithmetic.Sub(3),
                new Arithmetic(),
                Arithmetic.Add(-1));

            Block<Arithmetic> blockB = await fx.Mine();
            ActionEvaluation[] evalsB = ActionEvaluator<DumbAction>.EvaluateGradually(
                blockB.Hash,
                blockIndex: blockB.Index,
                txid: b.Id,
                previousStates: fx.CreateAccountStateDelta(0, blockB.PreviousHash),
                miner: blockB.Miner ?? default,
                signer: b.Signer,
                signature: b.Signature,
                actions: b.Actions.ToImmutableArray<IAction>(),
                rehearsal: rehearsal,
                previousBlockStatesTrie: fx.GetTrie(blockB.PreviousHash),
                blockAction: false).ToArray();

            Assert.Equal(evalsB.Length, deltaB.Count - 1);
            for (int i = 0; i < evalsB.Length; i++)
            {
                ActionEvaluation eval = evalsB[i];
                _logger.Debug("evalsB[{0}] = {@1}", i, eval);
                _logger.Debug("b.Actions[{0}] = {@1}", i, b.Actions[i]);
                Assert.Equal(b.Actions[i], eval.Action);
                IActionContext context = eval.InputContext;
                Assert.Equal(b.Id, context.TxId);
                Assert.Equal(blockB.Miner, context.Miner);
                Assert.Equal(blockB.Index, context.BlockIndex);
                Assert.Equal(
                    deltaB[i].RootHash,
                    context.PreviousStateRootHash);
                Assert.Equal(b.Signer, context.Signer);
                Assert.False(context.BlockAction);
                Assert.Equal(rehearsal, context.Rehearsal);
                IAccountStateDelta prevStates = context.PreviousStates;
                Assert.Equal(
                    i > 0 ? new[] { b.Signer } : new Address[0],
                    prevStates.UpdatedAddresses);
                Assert.Equal((Integer)deltaB[i].Value, prevStates.GetState(b.Signer));
                IAccountStateDelta outputStates = eval.OutputStates;
                Assert.Equal(new[] { b.Signer }, outputStates.UpdatedAddresses);
                Assert.Equal((Integer)deltaB[i + 1].Value, outputStates.GetState(b.Signer));
                if (i == 1)
                {
                    Assert.IsType<UnexpectedlyTerminatedActionException>(eval.Exception);
                    Assert.IsType<InvalidOperationException>(eval.Exception.InnerException);
                }
                else
                {
                    Assert.Null(eval.Exception);
                }
            }
        }

        [Fact]
        public void EvaluatePolicyBlockAction()
        {
            var store = new DefaultStore(null);
            var stateStore =
                new TrieStateStore(new MemoryKeyValueStore(), new MemoryKeyValueStore());
            var chain = TestUtils.MakeBlockChain<DumbAction>(
                policy: _policy,
                store: _storeFx.Store,
                stateStore: _storeFx.StateStore,
                genesisBlock: _storeFx.GenesisBlock);
            (var addresses, Transaction<DumbAction>[] txs) =
                MakeFixturesForAppendTests();
            var genesis = chain.Genesis;
            var block = TestUtils.MineNext(
                genesis,
                txs,
                difficulty: chain.Policy.GetNextBlockDifficulty(chain)).AttachStateRootHash(
                    chain.StateStore,
                    _policy.BlockAction);
            var stateCompleterSet = StateCompleterSet<DumbAction>.Recalculate;

            AccountStateGetter accountStateGetter = address => null;
            AccountBalanceGetter accountBalanceGetter =
                (address, currency) => new FungibleAssetValue(currency);
            IAccountStateDelta previousStates = block.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    accountStateGetter,
                    accountBalanceGetter,
                    genesis.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    accountStateGetter,
                    accountBalanceGetter,
                    genesis.Miner.GetValueOrDefault());
            var evaluation = chain.ActionEvaluator.EvaluatePolicyBlockAction(
                genesis,
                previousStates,
                null);
            Assert.Equal(chain.Policy.BlockAction, evaluation.Action);
            Assert.Equal(
                (Integer)1,
                (Integer)evaluation.OutputStates.GetState(genesis.Miner.GetValueOrDefault()));
            Assert.True(evaluation.InputContext.BlockAction);

            accountStateGetter = address => chain.GetState(
                address,
                block.PreviousHash,
                stateCompleterSet.StateCompleter);
            accountBalanceGetter =
                (address, currency) => chain.GetBalance(
                    address,
                    currency,
                    block.PreviousHash,
                    stateCompleterSet.FungibleAssetStateCompleter);
            previousStates = block.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    accountStateGetter, accountBalanceGetter, block.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    accountStateGetter, accountBalanceGetter, block.Miner.GetValueOrDefault());
            evaluation = chain.ActionEvaluator.EvaluatePolicyBlockAction(
                block,
                previousStates,
                null);
            Assert.Equal(chain.Policy.BlockAction, evaluation.Action);
            Assert.Equal(
                (Integer)2,
                (Integer)evaluation.OutputStates.GetState(block.Miner.GetValueOrDefault()));
            Assert.True(evaluation.InputContext.BlockAction);

            chain.ExecuteActions(block);
            chain.Append(
                block,
                DateTimeOffset.UtcNow,
                evaluateActions: false,
                renderBlocks: true,
                renderActions: false);
            previousStates = block.ProtocolVersion > 0
                ? new AccountStateDeltaImpl(
                    address => chain.GetState(address, block.PreviousHash),
                    _nullAccountBalanceGetter,
                    block.Miner.GetValueOrDefault())
                : new AccountStateDeltaImplV0(
                    address => chain.GetState(address, block.PreviousHash),
                    _nullAccountBalanceGetter,
                    block.Miner.GetValueOrDefault());
            var txEvaluations = chain.ActionEvaluator.EvaluateTxsGradually(
                block,
                previousStates).Select(te => te.Item2).ToList();
            previousStates = txEvaluations.Last().OutputStates;
            evaluation = chain.ActionEvaluator.EvaluatePolicyBlockAction(
                block,
                previousStates,
                null);
            Assert.Equal(
                (Integer)2,
                (Integer)evaluation.OutputStates.GetState(block.Miner.GetValueOrDefault()));
        }

        private (Address[], Transaction<DumbAction>[]) MakeFixturesForAppendTests(
            PrivateKey privateKey = null,
            DateTimeOffset epoch = default)
        {
            Address[] addresses =
            {
                _storeFx.Address1,
                _storeFx.Address2,
                _storeFx.Address3,
                _storeFx.Address4,
                _storeFx.Address5,
            };

            privateKey = privateKey ?? new PrivateKey(new byte[]
            {
                0xa8, 0x21, 0xc7, 0xc2, 0x08, 0xa9, 0x1e, 0x53, 0xbb, 0xb2,
                0x71, 0x15, 0xf4, 0x23, 0x5d, 0x82, 0x33, 0x44, 0xd1, 0x16,
                0x82, 0x04, 0x13, 0xb6, 0x30, 0xe7, 0x96, 0x4f, 0x22, 0xe0,
                0xec, 0xe0,
            });

            Transaction<DumbAction>[] txs =
            {
                _storeFx.MakeTransaction(
                    new[]
                    {
                        new DumbAction(addresses[0], "foo"),
                        new DumbAction(addresses[1], "bar"),
                    },
                    timestamp: epoch,
                    nonce: 0,
                    privateKey: privateKey),
                _storeFx.MakeTransaction(
                    new[]
                    {
                        new DumbAction(addresses[2], "baz"),
                        new DumbAction(addresses[3], "qux"),
                    },
                    timestamp: epoch.AddSeconds(5),
                    nonce: 1,
                    privateKey: privateKey),
            };

            return (addresses, txs);
        }

        private sealed class TestEvaluateAction : IAction
        {
            public static readonly Address SignerKey = new PrivateKey().ToAddress();
            public static readonly Address MinerKey = new PrivateKey().ToAddress();
            public static readonly Address BlockIndexKey = new PrivateKey().ToAddress();

            public IValue PlainValue => default(Dictionary);

            public void LoadPlainValue(IValue plainValue)
            {
            }

            public IAccountStateDelta Execute(IActionContext context) =>
                context.PreviousStates
                    .SetState(SignerKey, (Text)context.Signer.ToHex())
                    .SetState(MinerKey, (Text)context.Miner.ToHex())
                    .SetState(BlockIndexKey, (Integer)context.BlockIndex);
        }
    }
}
