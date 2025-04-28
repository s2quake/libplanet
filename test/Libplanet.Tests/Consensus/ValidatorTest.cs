// using System.Numerics;
// using Libplanet.Crypto;
// using Libplanet.Types.Consensus;
// using Xunit;

// namespace Libplanet.Tests.Consensus
// {
//     public class ValidatorTest
//     {
//         [Fact]
//         public void Constructor()
//         {
//             var publicKey = new PrivateKey().PublicKey;
//             var power = new BigInteger(1);
//             var validator = Validator.Create(publicKey, power);
//             Assert.Equal(publicKey, validator.PublicKey);
//             Assert.Equal(power, validator.Power);

//             var negativePower = new BigInteger(-1);
//             Assert.Throws<ArgumentOutOfRangeException>(
//                 () => Validator.Create(publicKey, negativePower));
//         }

//         [Fact]
//         public void Marshalling()
//         {
//             PublicKey publicKey = new PrivateKey().PublicKey;
//             Validator validator = Validator.Create(publicKey, BigInteger.One);
//             Validator unmarshalledValidator = Validator.Create(validator.Bencoded);
//             Assert.Equal(validator, unmarshalledValidator);
//             Assert.Equal(validator.PublicKey, unmarshalledValidator.PublicKey);
//             Assert.Equal(validator.Power, unmarshalledValidator.Power);
//         }
//     }
// }
