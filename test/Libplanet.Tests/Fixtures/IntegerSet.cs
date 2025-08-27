// using System.Security.Cryptography;
// using Libplanet.State;
// using Libplanet.Serialization;
// using Libplanet.Data;
// using Libplanet.Data.Structures;
// using Libplanet.Types;
// // using static Libplanet.State.SystemAddresses;

// namespace Libplanet.Tests.Fixtures;

// public sealed class IntegerSet
// {
//     public readonly IReadOnlyList<PrivateKey> PrivateKeys;
//     public readonly IReadOnlyList<Address> Addresses;
//     public readonly IReadOnlyList<Arithmetic> Actions;
//     public readonly ImmutableSortedSet<Transaction> Txs;
//     public readonly PrivateKey Proposer;
//     public readonly Block Genesis;
//     public readonly Libplanet.Blockchain Chain;
//     public readonly Repository Repository;

//     public IntegerSet(int[] initialStates)
//         : this([.. initialStates.Select(s => new BigInteger(s))], null)
//     {
//     }

//     public IntegerSet(
//         BigInteger[] initialStates,
//         BlockchainOptions? policy = null)
//     {
//         PrivateKeys = initialStates.Select(_ => new PrivateKey()).ToImmutableArray();
//         Addresses = PrivateKeys.Select(key => key.Address).ToImmutableArray();
//         Actions = initialStates
//             .Select((state, index) => new { State = state, Key = PrivateKeys[index] })
//             .Select(pair => new { pair.State, pair.Key })
//             .Select(pair => Arithmetic.Add(pair.State)).ToImmutableArray();
//         Txs = initialStates
//             .Select((state, index) => new { State = state, Key = PrivateKeys[index] })
//             .Select(pair => new { pair.State, pair.Key })
//             .Select(pair => new { Action = Arithmetic.Add(pair.State), pair.Key })
//             .Select(pair =>
//                 new TransactionMetadata
//                 {
//                     Signer = pair.Key.Address,
//                     Actions = new IAction[] { pair.Action }.ToBytecodes(),
//                 }.Sign(pair.Key))
//             .OrderBy(tx => tx.Id)
//             .ToImmutableSortedSet();
//         Proposer = new PrivateKey();
//         policy ??= new BlockchainOptions();
//         Genesis = TestUtils.ProposeGenesis(
//                 Proposer,
//                 transactions: Txs,
//                 timestamp: DateTimeOffset.UtcNow,
//                 protocolVersion: BlockHeader.CurrentProtocolVersion).Sign(Proposer);
//         Repository = new Repository(new MemoryDatabase());
//         Chain = new Blockchain(Genesis, Repository, policy);
//     }

//     public int Count => Addresses.Count;

//     // public BlockchainOptions Policy => Chain.Options;

//     public Block Tip => Chain.Tip;

//     public TxWithContext Sign(PrivateKey signerKey, params Arithmetic[] actions)
//     {
//         var signer = signerKey.Address;
//         string rawStateKey = signer.ToString();
//         long nonce = Chain.GetNextTxNonce(signer);
//         Transaction tx = new TransactionMetadata
//         {
//             Nonce = nonce,
//             Signer = signerKey.Address,
//             GenesisBlockHash = Genesis.BlockHash,
//             Actions = actions.ToBytecodes(),
//         }.Sign(signerKey);
//         BigInteger prevState = Chain.GetWorld().GetValueOrDefault(SystemAccount, signer, BigInteger.Zero);
//         HashDigest<SHA256> prevStateRootHash = Chain.Tip.PreviousStateRootHash;
//         Trie prevTrie = GetTrie(Chain.Tip.BlockHash);
//         (BigInteger, HashDigest<SHA256>) prevPair = (prevState, prevStateRootHash);
//         (BigInteger, HashDigest<SHA256>) stagedStates = Chain.StagedTransactions.Collect()
//             .Where(t => t.Signer.Equals(signer))
//             .OrderBy(t => t.Nonce)
//             .SelectMany(t => t.Actions)
//             .Aggregate(prevPair, (prev, act) =>
//             {
//                 var a = ModelSerializer.DeserializeFromBytes<Arithmetic>(act.Bytes.AsSpan());
//                 BigInteger nextState = a.Operator.ToFunc()(prev.Item1, a.Operand);
//                 var updatedRawStates = ImmutableDictionary<string, BigInteger>.Empty
//                     .Add(rawStateKey, nextState);
//                 HashDigest<SHA256> nextRootHash = Repository.States.Commit(
//                     updatedRawStates.Aggregate(
//                         prevTrie,
//                         (trie, pair) => trie.Set(pair.Key, ModelSerializer.SerializeToBytes(pair.Value)))).Hash;
//                 return (nextState, nextRootHash);
//             });
//         Chain.StagedTransactions.Add(tx);
//         ImmutableArray<(BigInteger, HashDigest<SHA256>)> expectedDelta = tx.Actions
//             .Aggregate(
//                 ImmutableArray.Create(stagedStates),
//                 (delta, act) =>
//                 {
//                     var a = ModelSerializer.DeserializeFromBytes<Arithmetic>(act.Bytes.AsSpan());
//                     BigInteger nextState =
//                         a.Operator.ToFunc()(delta[delta.Length - 1].Item1, a.Operand);
//                     var updatedRawStates = ImmutableDictionary<string, BigInteger>.Empty
//                         .Add(rawStateKey, nextState);
//                     HashDigest<SHA256> nextRootHash = Repository.States.Commit(
//                         updatedRawStates.Aggregate(
//                             prevTrie,
//                             (trie, pair) => trie.Set(pair.Key, ModelSerializer.SerializeToBytes(pair.Value)))).Hash;
//                     return delta.Add((nextState, nextRootHash));
//                 });
//         return new TxWithContext()
//         {
//             Tx = tx,
//             ExpectedDelta = expectedDelta,
//         };
//     }

//     public TxWithContext Sign(int signerIndex, params Arithmetic[] actions)
//         => Sign(PrivateKeys[signerIndex], actions);

//     public Block Propose() => Chain.ProposeBlock(Proposer);

//     public void Append(Block block) => Chain.Append(block, TestUtils.CreateBlockCommit(block));

//     public Trie GetTrie(BlockHash blockHash)
//     {
//         if (blockHash != default)
//         {
//             return Repository.States.GetTrie(Chain.Blocks[blockHash].PreviousStateRootHash);
//         }

//         return Repository.States.GetTrie(default);
//     }

//     public struct TxWithContext
//     {
//         public Transaction Tx;
//         public IReadOnlyList<(BigInteger Value, HashDigest<SHA256> RootHash)> ExpectedDelta;

//         public void Deconstruct(
//             out Transaction tx,
//             out IReadOnlyList<(BigInteger Value, HashDigest<SHA256> RootHash)> expectedDelta)
//         {
//             tx = Tx;
//             expectedDelta = ExpectedDelta;
//         }
//     }
// }
