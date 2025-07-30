// // using Libplanet.Types.Crypto;
// using Libplanet.Types.Evidence;
// using Xunit;

// namespace Libplanet.Tests.Blockchain.Evidence
// {
//     public class EvidenceTest
//     {
//         [Fact]
//         public void Create_WithNegativeHeight_FailTest()
//         {
//             Assert.Throws<ArgumentOutOfRangeException>(
//                 paramName: "height",
//                 testCode: () => TestEvidence.Create(-1, default, DateTimeOffset.UtcNow));
//         }

//         [Fact]
//         public void Create_WithNotDictionary_FailTest()
//         {
//             Assert.Throws<ArgumentException>(
//                 paramName: "bencoded",
//                 testCode: () => TestEvidence.Create(new Bencodex.Types.Integer(0)));
//         }

//         [Fact]
//         public void Decode_WithInvalidEvidenceWithoutConstructor_FailTest()
//         {
//             var height = 0L;
//             var address = new PrivateKey().Address;
//             var timestamp = DateTimeOffset.UtcNow;
//             var evidence = TestEvidence.Create(height, address, timestamp);

//             var bencoded = EvidenceBase.Bencode(evidence);
//             if (bencoded is Dictionary dictionary)
//             {
//                 var typeName = EvidenceBase.GetTypeName(typeof(InvalidEvidence));
//                 bencoded = dictionary.SetItem("type", new Text(typeName));
//             }

//             Assert.Throws<NotSupportedException>(
//                 testCode: () => EvidenceBase.Decode(bencoded));
//         }

//         [Fact]
//         public void GetTypeName_Test()
//         {
//             var evidenceType = typeof(InvalidEvidence);
//             var actualTypeName = EvidenceBase.GetTypeName(evidenceType);
//             var expectedTypeName
//                 = $"{evidenceType.FullName}, {evidenceType.Assembly.GetName().Name}";
//             Assert.Equal(expectedTypeName, actualTypeName);
//         }

//         private sealed record class InvalidEvidence : EvidenceBase
//         {
//             protected override void OnVerify(IEvidenceContext evidenceContext)
//             {
//             }
//         }
//     }
// }
