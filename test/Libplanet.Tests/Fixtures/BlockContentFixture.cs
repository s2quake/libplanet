// using System.Security.Cryptography;
// // using Libplanet.State;
// using Libplanet.State.Sys;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Tests.Blockchain.Evidence;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;
// using Xunit;

// namespace Libplanet.Tests.Fixtures
// {
//     public class BlockContentFixture
//     {
//         public readonly PrivateKey GenesisKey;
//         public readonly BlockHash GenesisHash;
//         public readonly BlockContent GenesisContent;
//         public readonly BlockHeader GenesisMetadata;

//         public readonly PrivateKey Block1Key;
//         public readonly PrivateKey Block1Tx0Key;
//         public readonly PrivateKey Block1Tx1Key;
//         public readonly Transaction Block1Tx0;
//         public readonly Transaction Block1Tx1;
//         public readonly EvidenceBase Block1Ev0;
//         public readonly EvidenceBase Block1Ev1;
//         public readonly BlockContent Block1Content;
//         public readonly HashDigest<SHA256> Block1TxHash;
//         public readonly BlockHeader Block1Metadata;

//         public readonly BlockContent GenesisContentPv0;
//         public readonly BlockHeader GenesisMetadataPv0;
//         public readonly BlockContent Block1ContentPv1;
//         public readonly BlockHeader Block1MetadataPv1;

//         public BlockContentFixture()
//         {
//             TimeSpan kst = TimeSpan.FromHours(9);
//             GenesisKey = PrivateKey.Parse(
//                 "9bf4664ba09a89faeb684b94e69ffde01d26ae14b556204d3f6ab58f61f78418");
//             Transaction genTx = new TransactionMetadata
// {
//                 0,
//                 GenesisKey,
//                 null,
//                 actions: new IAction[]
//                     {
//                         new Initialize
//                         {
//                             Validators = TestUtils.Validators,
//                         },
//                     }.ToPlainValues(),
//                 timestamp: DateTimeOffset.MinValue
//             );
//             Transaction[] genTxs = new[] { genTx };
//             EvidenceBase[] genEvidence = new[]
//             {
//                 new TestEvidence(
//                     0, GenesisKey.Address, new DateTimeOffset(2024, 5, 24, 14, 12, 9, 45, kst)),
//             };
//             GenesisContent = new BlockContent
//             {
//                 Metadata = new BlockHeader
//                 {
//                     Index = 0,
//                     Timestamp = new DateTimeOffset(2021, 9, 6, 13, 46, 39, 123, kst),
//                     PublicKey = GenesisKey.PublicKey,
//                     PreviousHash = default,
//                     TxHash = BlockContent.DeriveTxHash(genTxs),
//
//                     EvidenceHash = BlockContent.DeriveEvidenceHash(genEvidence),
//                 },
//                 Transactions = [.. genTxs],
//                 Evidence = [.. genEvidence],
//             };
//             GenesisMetadata = GenesisContent.Metadata;
//             GenesisHash = BlockHash.Parse(
//                 "341e8f360597d5bc45ab96aabc5f1b0608063f30af7bd4153556c9536a07693a");

//             Block1Key = PrivateKey.Parse(
//                 "fcf30b333d04ccfeb562f000a32df488e7154949d31ddcac3cf9278acb5786c7");
//             Block1Tx0Key = PrivateKey.Parse(
//                 "2d5c20079bc4b2e6eab9ecbb405da8ba6590c436edfb07b7d4466563d7dac096");
//             Block1Tx0 = new Transaction(
//                 new UnsignedTx(
//                     new TxInvoice
//                     {
//                         GenesisHash = GenesisHash,
//                         UpdatedAddresses = [Block1Tx0Key.Address],
//                         Timestamp = new DateTimeOffset(2021, 9, 6, 17, 0, 1, 1, default),
//                         Actions = [.. ImmutableArray.Create<IAction>([
//                             Arithmetic.Add(10), Arithmetic.Add(50), Arithmetic.Sub(25),
//                         ]).ToPlainValues()],
//                     },
//                     new TxSigningMetadata(Block1Tx0Key.PublicKey, nonce: 0L)
//                 ),
//                 Signature: ByteUtil.ParseHexToImmutable(
//                     "6054008481425278039421becc25fcb030d12714dc53490fdd5d43bcc8fe2d2a5" +
//                     "b80a751bbde0b4813bd94ecd8e63bafee4f18db787beb117a5fc1aa5c2af9ca1b"
//                 )
//             );
//             Block1Tx1Key = PrivateKey.Parse(
//                 "105341c78dfb0dd313b961081630444c2586a1f01fb0c625368ffdc9136cfa30");
//             Block1Tx1 = new Transaction(
//                 new UnsignedTx(
//                     new TxInvoice
//                     {
//                         GenesisHash = GenesisHash,
//                         UpdatedAddresses = [Block1Tx1Key.Address],
//                         Timestamp = new DateTimeOffset(2021, 9, 6, 17, 0, 1, 1, default),
//                         Actions = [.. ImmutableArray.Create<IAction>([
//                             Arithmetic.Add(30),
//                         ]).ToPlainValues()],
//                     },
//                     new TxSigningMetadata(Block1Tx1Key.PublicKey, nonce: 1L)),
//                 Signature: ByteUtil.ParseHexToImmutable(
//                     "f74609d91b9c5348ba248df1dcbce4114031971beb39c12874df3a8c4c8651540" +
//                     "b31c9460da2b673cae61502036f6054353303be3c0b210ad9659afc6e9f5ce71b")
//             );
//             Block1Ev0 = new TestEvidence(
//                 0, GenesisKey.Address, new DateTimeOffset(2024, 5, 24, 14, 13, 9, 45, kst));
//             Block1Ev1 = new TestEvidence(
//                 0, GenesisKey.Address, new DateTimeOffset(2024, 5, 24, 14, 14, 9, 45, kst));

//             var block1Transactions = new List<Transaction>() { Block1Tx0, Block1Tx1 }
//                 .OrderBy(tx => tx.Id).ToList();
//             var block1Evidence = new List<EvidenceBase>() { Block1Ev0, Block1Ev1 }
//                 .OrderBy(tx => tx.Id).ToList();
//             Block1Content = new BlockContent
//             {
//                 Metadata = new BlockHeader
//                 {
//                     Index = 1,
//                     Timestamp = new DateTimeOffset(2021, 9, 6, 17, 1, 9, 45, kst),
//                     PublicKey = Block1Key.PublicKey,
//                     PreviousHash = GenesisHash,
//                     TxHash = BlockContent.DeriveTxHash(block1Transactions),
//
//                     EvidenceHash = BlockContent.DeriveEvidenceHash(block1Evidence),
//                 },
//                 Transactions = [.. block1Transactions],
//                 Evidence = [.. block1Evidence],
//             };
//             Block1TxHash = HashDigest<SHA256>.Parse(
//                 "9d6457e7bdc4b19d1f341c45c787cf80a17c514da10d702606cc41f23387badb");
//             Block1Metadata = Block1Content.Metadata;

//             GenesisContentPv0 = new BlockContent
//             {
//                 Metadata = new BlockHeader
//                 {
//                     ProtocolVersion = 0,
//                     Index = 0,
//                     Timestamp = new DateTimeOffset(2021, 9, 6, 13, 46, 39, 123, kst),
//                     Miner = GenesisKey.Address,
//                     PublicKey = null,
//                     PreviousHash = default,
//
//                     EvidenceHash = null,
//                 },
//                 Transactions = [],
//                 Evidence = [],
//             }; // Tweaked GenesisContent
//             GenesisMetadataPv0 = GenesisContentPv0.Metadata;
//             Block1ContentPv1 = new BlockContent
//             {
//                 Metadata = new BlockHeader
//                 {
//                     ProtocolVersion = 1,
//                     Index = 1,
//                     Timestamp = new DateTimeOffset(2021, 9, 6, 17, 1, 9, 45, kst),
//                     Miner = Block1Key.Address,
//                     PublicKey = null,
//                     PreviousHash = GenesisHash,
//                     TxHash = BlockContent.DeriveTxHash(block1Transactions),
//
//                     EvidenceHash = BlockContent.DeriveEvidenceHash(block1Evidence),
//                 },
//                 Transactions = [.. block1Transactions],
//                 Evidence = [.. block1Evidence],
//             }; // Tweaked Block1Content
//             Block1MetadataPv1 = Block1ContentPv1.Metadata;
//         }

//         [Fact]
//         public void ValidateBlockContentFixture()
//         {
//             Assert.Equal(Block1TxHash, Block1Content.TxHash);
//             Assert.Equal(Block1TxHash, Block1ContentPv1.TxHash);
//         }
//     }
// }
