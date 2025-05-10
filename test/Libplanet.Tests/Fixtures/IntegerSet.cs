using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;
using static Libplanet.Action.State.ReservedAddresses;

namespace Libplanet.Tests.Fixtures;

public sealed class IntegerSet
{
    public readonly IReadOnlyList<PrivateKey> PrivateKeys;
    public readonly IReadOnlyList<Address> Addresses;
    public readonly IReadOnlyList<Arithmetic> Actions;
    public readonly ImmutableSortedSet<Transaction> Txs;
    public readonly PrivateKey Proposer;
    public readonly Block Genesis;
    public readonly BlockChain Chain;
    public readonly IStore Store;
    public readonly IKeyValueStore KVStore;
    public readonly TrieStateStore StateStore;

    public IntegerSet(int[] initialStates)
        : this([.. initialStates.Select(s => new BigInteger(s))], null)
    {
    }

    public IntegerSet(
        BigInteger[] initialStates,
        BlockChainOptions? policy = null)
    {
        PrivateKeys = initialStates.Select(_ => new PrivateKey()).ToImmutableArray();
        Addresses = PrivateKeys.Select(key => key.Address).ToImmutableArray();
        Actions = initialStates
            .Select((state, index) => new { State = state, Key = PrivateKeys[index] })
            .Select(pair => new { pair.State, pair.Key })
            .Select(pair => Arithmetic.Add(pair.State)).ToImmutableArray();
        Txs = initialStates
            .Select((state, index) => new { State = state, Key = PrivateKeys[index] })
            .Select(pair => new { pair.State, pair.Key })
            .Select(pair => new { Action = Arithmetic.Add(pair.State), pair.Key })
            .Select(pair =>
                Transaction.Create(
                    unsignedTx: new UnsignedTx
                    {
                        Invoice = new TxInvoice
                        {
                            Actions = new IAction[] { pair.Action }.ToBytecodes(),
                        },
                        SigningMetadata = new TxSigningMetadata
                        {
                            Signer = pair.Key.Address,
                        },
                    },
                    privateKey: pair.Key))
            .OrderBy(tx => tx.Id)
            .ToImmutableSortedSet();
        Proposer = new PrivateKey();
        policy ??= new BlockChainOptions();
        Store = new Libplanet.Store.Store(new MemoryDatabase());
        KVStore = new MemoryKeyValueStore();
        StateStore = new TrieStateStore(KVStore);
        var actionEvaluator = new ActionEvaluator(
            StateStore,
            policy.PolicyActions);
        Genesis = TestUtils.ProposeGenesisBlock(
            TestUtils.ProposeGenesis(
                Proposer.PublicKey,
                Txs,
                null,
                DateTimeOffset.UtcNow,
                BlockHeader.CurrentProtocolVersion),
            Proposer);
        Chain = BlockChain.Create(Genesis, policy);
    }

    public int Count => Addresses.Count;

    public BlockChainOptions Policy => Chain.Options;

    public Block Tip => Chain.Tip;

    public TxWithContext Sign(PrivateKey signerKey, params Arithmetic[] actions)
    {
        var signer = signerKey.Address;
        KeyBytes rawStateKey = KeyConverters.ToStateKey(signer);
        long nonce = Chain.GetNextTxNonce(signer);
        Transaction tx = Transaction.Create(nonce, signerKey, Genesis.BlockHash, actions.ToBytecodes());
        BigInteger prevState = Chain.GetNextWorld().GetValueOrFallback(LegacyAccount, signer, BigInteger.Zero);
        HashDigest<SHA256> prevStateRootHash = Chain.Tip.StateRootHash;
        ITrie prevTrie = GetTrie(Chain.Tip.BlockHash);
        (BigInteger, HashDigest<SHA256>) prevPair = (prevState, prevStateRootHash);
        (BigInteger, HashDigest<SHA256>) stagedStates = Chain.ListStagedTransactions()
            .Where(t => t.Signer.Equals(signer))
            .OrderBy(t => t.Nonce)
            .SelectMany(t => t.Actions)
            .Aggregate(prevPair, (prev, act) =>
            {
                var a = ModelSerializer.DeserializeFromBytes<Arithmetic>(act.Bytes);
                BigInteger nextState = a.Operator.ToFunc()(prev.Item1, a.Operand);
                var updatedRawStates = ImmutableDictionary<KeyBytes, BigInteger>.Empty
                    .Add(rawStateKey, nextState);
                HashDigest<SHA256> nextRootHash = Chain.StateStore.Commit(
                    updatedRawStates.Aggregate(
                        prevTrie,
                        (trie, pair) => trie.Set(pair.Key, ModelSerializer.Serialize(pair.Value)))).Hash;
                return (nextState, nextRootHash);
            });
        Chain.StageTransaction(tx);
        ImmutableArray<(BigInteger, HashDigest<SHA256>)> expectedDelta = tx.Actions
            .Aggregate(
                ImmutableArray.Create(stagedStates),
                (delta, act) =>
                {
                    var a = ModelSerializer.DeserializeFromBytes<Arithmetic>(act.Bytes);
                    BigInteger nextState =
                        a.Operator.ToFunc()(delta[delta.Length - 1].Item1, a.Operand);
                    var updatedRawStates = ImmutableDictionary<KeyBytes, BigInteger>.Empty
                        .Add(rawStateKey, nextState);
                    HashDigest<SHA256> nextRootHash = Chain.StateStore.Commit(
                        updatedRawStates.Aggregate(
                            prevTrie,
                            (trie, pair) => trie.Set(pair.Key, ModelSerializer.Serialize(pair.Value)))).Hash;
                    return delta.Add((nextState, nextRootHash));
                });
        return new TxWithContext()
        {
            Tx = tx,
            ExpectedDelta = expectedDelta,
        };
    }

    public TxWithContext Sign(int signerIndex, params Arithmetic[] actions)
        => Sign(PrivateKeys[signerIndex], actions);

    public Block Propose() => Chain.ProposeBlock(Proposer, TestUtils.CreateBlockCommit(Chain.Tip));

    public void Append(Block block) => Chain.Append(block, TestUtils.CreateBlockCommit(block));

    public ITrie GetTrie(BlockHash blockHash)
    {
        if (blockHash != default)
        {
            return StateStore.GetStateRoot(Store.GetBlockDigest(blockHash).StateRootHash);
        }

        return StateStore.GetStateRoot(default);
    }

    public struct TxWithContext
    {
        public Transaction Tx;
        public IReadOnlyList<(BigInteger Value, HashDigest<SHA256> RootHash)> ExpectedDelta;

        public void Deconstruct(
            out Transaction tx,
            out IReadOnlyList<(BigInteger Value, HashDigest<SHA256> RootHash)> expectedDelta)
        {
            tx = Tx;
            expectedDelta = ExpectedDelta;
        }
    }
}
