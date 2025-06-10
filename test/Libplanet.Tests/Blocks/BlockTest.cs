// using System.Security.Cryptography;
// // using Libplanet.State;
// using Libplanet.State.Tests.Actions;
// using Libplanet.Types;
// using Libplanet.Tests.Fixtures;
// using Libplanet.Types;
// using Libplanet.Types;
// using Xunit;
// using Xunit.Abstractions;
// using static Libplanet.Tests.TestUtils;

// namespace Libplanet.Tests.Blocks
// {
//     public class BlockTest : IClassFixture<BlockFixture>
//     {
//         private BlockFixture _fx;
//         private ITestOutputHelper _output;

//         public BlockTest(BlockFixture fixture, ITestOutputHelper output)
//         {
//             _fx = fixture;
//             _output = output;
//         }

//         [Fact]
//         public void Constructor()
//         {
//             var contents = new BlockContentFixture();
//             var random = new System.Random();
//             var stateRootHash = random.NextHashDigest<SHA256>();
//             RawBlock preEval = contents.GenesisContent.Propose();
//             ImmutableArray<byte> signature =
//                 preEval.Header.MakeSignature(contents.GenesisKey, stateRootHash);
//             var hash = preEval.Header.DeriveBlockHash(stateRootHash, signature);
//             var block = new Block(preEval, (stateRootHash, signature, hash));
//             AssertPreEvaluationBlocksEqual(preEval, block);
//             AssertBytesEqual(stateRootHash, block.StateRootHash);
//             AssertBytesEqual(signature, block.Signature?.ToArray());
//         }

//         [Fact]
//         public void CompareToOtherBlock()
//         {
//             Block sameBlock1 = _fx.Genesis;
//             var sameBlock2 = BlockMarshaler.UnmarshalBlock(_fx.Genesis.MarshalBlock());
//             Block differentBlock = _fx.Next;

//             Assert.Equal(sameBlock1, sameBlock2);
//             Assert.NotEqual(sameBlock2, differentBlock);

//             Assert.True(sameBlock1.Equals(sameBlock2));
//             Assert.False(sameBlock2.Equals(differentBlock));
//         }

//         [Fact]
//         public void TransactionOrderIdempotent()
//         {
//             const int signerCount = 5;
//             DateTimeOffset timestamp = DateTimeOffset.UtcNow;
//             var signers = Enumerable.Range(0, signerCount).Select(_ => new PrivateKey());
//             ImmutableArray<Transaction> txs = signers.Select(signer =>
//                 new TransactionMetadata
// {
//                     0,
//                     signer,
//                     null,
//                     new[]
//                     {
//                         new ContextRecordingAction(signer.Address, new Text("Foo")),
//                     }.ToPlainValues())).ToImmutableArray();
//             var blockA = ProposeGenesis(
//                 GenesisProposer.PublicKey,
//                 timestamp: timestamp,
//                 transactions: txs);
//             var blockB = ProposeGenesis(
//                 GenesisProposer.PublicKey,
//                 timestamp: timestamp,
//                 transactions: txs);

//             Assert.True(blockA.Transactions.SequenceEqual(blockB.Transactions));
//         }
//     }
// }
