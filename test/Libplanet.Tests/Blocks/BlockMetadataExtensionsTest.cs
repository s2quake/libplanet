// using Libplanet.Types.Crypto;
// using Libplanet.Tests.Fixtures;
// using Libplanet.Types.Blocks;
// using Xunit;

// namespace Libplanet.Tests.Blocks
// {
//     public class BlockHeaderExtensionsTest : BlockContentFixture
//     {
//         [Fact]
//         public void ValidateTimestamp()
//         {
//             DateTimeOffset now = DateTimeOffset.UtcNow;
//             DateTimeOffset future = now + TimeSpan.FromSeconds(17);
//             PublicKey publicKey = new PrivateKey().PublicKey;
//             IBlockHeader metadata = new BlockHeader(
//                 index: 0,
//                 timestamp: future,
//                 publicKey: publicKey,
//                 previousHash: default,
//                 txHash: null,
//                 lastCommit: null,
//                 evidenceHash: null);
//             Assert.Throws<InvalidOperationException>(() => metadata.ValidateTimestamp(now));

//             // It's okay because 3 seconds later.
//             metadata.ValidateTimestamp(now + TimeSpan.FromSeconds(3));
//         }
//     }
// }
