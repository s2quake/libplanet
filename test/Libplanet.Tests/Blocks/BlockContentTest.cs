// using System.Security.Cryptography;
// using Libplanet.Common;
// using Libplanet.Crypto;
// using Libplanet.Tests.Fixtures;
// using Libplanet.Types.Blocks;
// using Libplanet.Types.Evidence;
// using Libplanet.Types.Tx;
// using Xunit;
// using Xunit.Abstractions;
// using static Libplanet.Tests.TestUtils;

// namespace Libplanet.Tests.Blocks
// {
//     public class BlockContentTest : BlockContentFixture
//     {
//         private readonly ITestOutputHelper _output;

//         public BlockContentTest(ITestOutputHelper output)
//         {
//             _output = output;
//         }

//         [Fact]
//         public void CopyConstructors()
//         {
//             var block1 = new BlockContent
//             {
//                 Metadata = Block1Metadata,
//                 Transactions = Block1Content.Transactions,
//                 Evidence = Block1Content.Evidence,
//             };
//             AssertBlockContentsEqual(Block1Content, block1);

//             Assert.Throws<InvalidOperationException>(() =>
//                 new BlockContent
//                 {
//                     Metadata = Block1Metadata,
//                     Transactions = [],
//                     Evidence = [],
//                 }
//             );
//             Assert.Throws<InvalidOperationException>(
//                 () => new BlockContent
//                 {
//                     Metadata = Block1Metadata,
//                     Transactions = [Block1Tx0],
//                     Evidence = [],
//                 }
//             );
//             Assert.Throws<InvalidOperationException>(() =>
//                 new BlockContent
//                 {
//                     Metadata = Block1Metadata,
//                     Transactions = Block1Content.Transactions,
//                     Evidence = [],
//                 }
//             );
//             Assert.Throws<InvalidOperationException>(
//                 () => new BlockContent
//                 {
//                     Metadata = Block1Metadata,
//                     Transactions = Block1Content.Transactions,
//                     Evidence = [],
//                 }
//             );
//         }

//         [Fact]
//         public void Transactions()
//         {
//             var key = PrivateKey.Parse(
//                 "ea0493b0ed67fc97b2e5e85a1d145adea294112f09df15398cb10f2ed5ad1a83"
//             );
//             var tx2 = new Transaction(
//                 new UnsignedTx(
//                     new TxInvoice
//                     {
//                         GenesisHash = GenesisHash,
//                         Timestamp = new DateTimeOffset(2021, 9, 7, 10, 23, 12, 345, default),
//                     },
//                     new TxSigningMetadata(key.PublicKey, nonce: 0)
//                 ),
//                 Signature: ByteUtil.ParseHexToImmutable(
//                     "cd51a992907121083ae2da9b39f94063fe5eb1bb91bb96dc83ced6add8370fe36" +
//                     "394d6db0fca60ebfe40284e4c4cf6096cf5aa4e18bdc5d4f732033ea692e7521c"
//                 )
//             );
//             var txs = new[] { tx2, Block1Tx0, Block1Tx1 }.OrderBy(tx => tx.Id).ToImmutableList();
//             var blockContent = new BlockContent
//             {
//                 Metadata = new BlockMetadata
//                 {
//                     Index = Block1Content.Index,
//                     Timestamp = DateTimeOffset.UtcNow,
//                     PublicKey = Block1Content.PublicKey,
//                     PreviousHash = Block1Content.PreviousHash,
//                     TxHash = BlockContent.DeriveTxHash(txs),
//                     
//                     EvidenceHash = null,
//                 },
//                 Transactions = [..txs],
//                 Evidence = [],
//             };
//             Assert.Equal(
//                 new[] { Block1Tx1.Id, Block1Tx0.Id, tx2.Id },
//                 blockContent.Transactions.Select(tx => tx.Id).ToArray());
//         }

//         [Fact]
//         public void TransactionsWithDuplicateNonce()
//         {
//             var dupTx1 = new Transaction(
//                 new UnsignedTx(
//                     new TxInvoice
//                     {
//                         GenesisHash = GenesisHash,
//                         UpdatedAddresses = [Block1Tx1.Signer],
//                         Timestamp = Block1Tx1.Timestamp,
//                         Actions = [],
//                     },
//                     new TxSigningMetadata(Block1Tx1.Signer, Nonce: 1L)
//                 ),
//                 Signature: ByteUtil.ParseHexToImmutable(
//                     "271c43e8c1a54c59686a49f13f1279765dd26a40a7b5e649a7dbf938bbcef3bf5" +
//                     "e1d6be5b456506873fbd9d3e5b07a5f72bfeac19774cd8f8c7fd4f4f73abb6d1b"
//                 )
//             );
//             var txs = new[] { Block1Tx0, Block1Tx1, dupTx1 }.OrderBy(tx => tx.Id).ToArray();
//             InvalidOperationException e = Assert.Throws<InvalidOperationException>(
//                 () => new BlockContent
//                 {
//                     Metadata = new BlockMetadata
//                     {
//                         Index = Block1Content.Index,
//                         Timestamp = DateTimeOffset.UtcNow,
//                         PublicKey = Block1Content.PublicKey,
//                         PreviousHash = Block1Content.PreviousHash,
//                         TxHash = BlockContent.DeriveTxHash(txs),
//                         
//                         EvidenceHash = null,
//                     },
//                     Transactions = [.. txs],
//                 });
//             Assert.Equal(dupTx1.Id, e.TxId);
//             Assert.Equal(2L, e.ExpectedNonce);
//             Assert.Equal(dupTx1.Nonce, e.ImproperNonce);
//         }

//         [Fact]
//         public void TransactionsWithMissingNonce()
//         {
//             var dupTx1 = new Transaction(
//                 new UnsignedTx(
//                     new TxInvoice
//                     {
//                         GenesisHash = GenesisHash,
//                         UpdatedAddresses = [Block1Tx1.Signer],
//                         Timestamp = Block1Tx1.Timestamp,
//                         Actions = [],
//                     },
//                     new TxSigningMetadata(Block1Tx1.Signer, Nonce: 3L)
//                 ),
//                 Signature: ByteUtil.ParseHexToImmutable(
//                     "299543707e52a2ba0a20f6dfd306ca6a87c8f0567134c83f1a078af064547b9f4" +
//                     "72f3e6d2bb88c7e7cc46a6d70017117f5cb75fbb5cbd7239e042d1273072e861c"
//                 )
//             );
//             var txs = new[] { Block1Tx1, Block1Tx0, dupTx1 }.OrderBy(tx => tx.Id).ToArray();
//             InvalidOperationException e = Assert.Throws<InvalidOperationException>(
//                 () => new BlockContent
//                 {
//                     Metadata = new BlockMetadata
//                     {
//                         Index = Block1Content.Index,
//                         Timestamp = DateTimeOffset.UtcNow,
//                         PublicKey = Block1Content.PublicKey,
//                         PreviousHash = Block1Content.PreviousHash,
//                         TxHash = BlockContent.DeriveTxHash(txs),
//                         
//                         EvidenceHash = null,
//                     },
//                     Transactions = [.. txs],
//                 });
//             Assert.Equal(dupTx1.Id, e.TxId);
//             Assert.Equal(2L, e.ExpectedNonce);
//             Assert.Equal(dupTx1.Nonce, e.ImproperNonce);
//         }

//         [Fact]
//         public void TransactionsWithInconsistentGenesisHashes()
//         {
//             var key = PrivateKey.Parse(
//                 "2ed05de0b35d93e4ae801ae40c8bb4257a771ff67c1e5d1754562e4191953710"
//             );
//             var differentGenesisHash = BlockHash.Parse(
//                 "76942b42f99c28da02ed916ebd2fadb189415e8288a4bd87f9ae3594127b79e6"
//             );
//             var txWithDifferentGenesis = new Transaction(
//                 new UnsignedTx(
//                     new TxInvoice
//                     {
//                         GenesisHash = differentGenesisHash,
//                         Timestamp = new DateTimeOffset(
//                             2021, 9, 7, 12, 1, 12, 345, TimeSpan.FromHours(9)),
//                         Actions = [],
//                     },
//                     new TxSigningMetadata(key.PublicKey, nonce: 0L)
//                 ),
//                 key
//             );
//             Transaction[] inconsistentTxs =
//                 Block1Content.Transactions.Append(txWithDifferentGenesis).ToArray();
//             InvalidTxGenesisHashException e = Assert.Throws<InvalidTxGenesisHashException>(
//                 () => new BlockContent
//                 {
//                     Metadata = new BlockMetadata
//                     {
//                         Index = Block1Content.Index,
//                         Timestamp = DateTimeOffset.UtcNow,
//                         PublicKey = Block1Content.PublicKey,
//                         PreviousHash = Block1Content.PreviousHash,
//                         TxHash = BlockContent.DeriveTxHash(inconsistentTxs),
//                         
//                         EvidenceHash = null,
//                     },
//                     Transactions = [.. inconsistentTxs],
//                 });
//             Assert.Equal(Block1Content.Transactions[0].GenesisHash, e.ExpectedGenesisHash);
//             Assert.Equal(differentGenesisHash, e.ImproperGenesisHash);
//         }

//         [Fact]
//         public void TxHash()
//         {
//             var expected = new HashDigest<SHA256>(new byte[]
//             {
//                 0x9d, 0x64, 0x57, 0xe7, 0xbd, 0xc4, 0xb1, 0x9d, 0x1f, 0x34, 0x1c, 0x45, 0xc7, 0x87,
//                 0xcf, 0x80, 0xa1, 0x7c, 0x51, 0x4d, 0xa1, 0x0d, 0x70, 0x26, 0x06, 0xcc, 0x41, 0xf2,
//                 0x33, 0x87, 0xba, 0xdb,
//             });
//             AssertBytesEqual(expected, Block1Content.TxHash);
//             Assert.Null(GenesisContentPv0.TxHash);
//         }

//         [Fact]
//         public void DeriveTxHash()
//         {
//             Assert.Null(
//                 BlockContent.DeriveTxHash([])
//             );
//             AssertBytesEqual(
//                 Block1Metadata.TxHash,
//                 BlockContent.DeriveTxHash(Block1Content.Transactions)
//             );
//             Assert.Throws<ArgumentException>(
//                 () => BlockContent.DeriveTxHash(Block1Content.Transactions.Reverse())
//             );
//         }
//     }
// }
