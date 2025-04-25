// using System.Security.Cryptography;
// using Libplanet.Common;
// using Libplanet.Types.Blocks;
// using Xunit;
// using static Libplanet.Tests.TestUtils;

// namespace Libplanet.Tests.Blocks;

// public class BlockHeaderTest : IClassFixture<BlockFixture>
// {
//     private BlockFixture _fx;

//     public BlockHeaderTest(BlockFixture fixture) => _fx = fixture;

//     [Fact]
//     public void Constructors()
//     {
//         Block[] fixtures = { _fx.Genesis, _fx.Next, _fx.HasTx };
//         foreach (Block fx in fixtures)
//         {
//             var preEval = fx.RawBlock.Header with { };
//             var header = new BlockHeader(preEval, fx.StateRootHash, fx.Signature, fx.Hash);
//             AssertBytesEqual(header.BlockHash, fx.Hash);
//             AssertPreEvaluationBlockHeadersEqual(fx, header);
//             AssertBytesEqual(fx.StateRootHash, header.StateRootHash);

//             Assert.Throws<InvalidOperationException>(() =>
//                 new BlockHeader(preEval, (fx.StateRootHash, fx.Signature, default))
//             );
//         }
//     }

//     [Fact]
//     public void ValidateSignature()
//     {
//         Block fx = _fx.HasTx;
//         var preEval = new RawBlockHeader(fx);
//         HashDigest<SHA256> arbitraryHash = new Random().NextHashDigest<SHA256>();
//         ImmutableArray<byte> invalidSig = preEval.MakeSignature(_fx.Miner, arbitraryHash);
//         InvalidOperationException e = Assert.Throws<InvalidOperationException>(() =>
//             new BlockHeader(
//                 preEval,
//                 (
//                     fx.StateRootHash,
//                     invalidSig,
//                     preEval.DeriveBlockHash(fx.StateRootHash, invalidSig)
//                 )));
//         Assert.Equal(invalidSig, e.InvalidSignature);
//         Assert.Equal(fx.PublicKey, e.PublicKey);

//         BlockHash hashWithInvalidSig = preEval.DeriveBlockHash(arbitraryHash, invalidSig);
//         e = Assert.Throws<InvalidOperationException>(() =>
//             new BlockHeader(preEval, (fx.StateRootHash, invalidSig, hashWithInvalidSig))
//         );
//         Assert.Equal(invalidSig, e.InvalidSignature);
//         Assert.Equal(fx.PublicKey, e.PublicKey);
//     }

//     [Fact]
//     public void ValidateHash()
//     {
//         Block fx = _fx.HasTx;
//         var preEval = new RawBlockHeader(fx);
//         ImmutableArray<byte> sig = fx.Signature.Value;
//         HashDigest<SHA256> arbitraryHash = new Random().NextHashDigest<SHA256>();
//         BlockHash invalidHash = preEval.DeriveBlockHash(arbitraryHash, sig);
//         Assert.Throws<InvalidOperationException>(() =>
//             new BlockHeader(preEval, (fx.StateRootHash, sig, invalidHash))
//         );
//     }

//     [Fact]
//     public void String()
//     {
//         var header = new BlockHeader(
//             new RawBlockHeader(_fx.HasTx),
//             (_fx.HasTx.StateRootHash, _fx.HasTx.Signature, _fx.HasTx.Hash));
//         Assert.Equal($"#{_fx.HasTx.Index} {_fx.HasTx.Hash}", header.ToString());
//     }
// }
