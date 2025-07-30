// using System.Numerics;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;
// using Xunit;

// namespace Libplanet.Tests.Consensus
// {
//     public class ValidatorSetTest
//     {
//         [Fact]
//         public void DuplicateValidatorNotAllowed()
//         {
//             List<PublicKey> publicKeys = Enumerable
//                 .Range(0, 5).Select(_ => new PrivateKey().PublicKey).ToList();
//             var validators = publicKeys
//                 .Select(publicKey => Validator.Create(publicKey, BigInteger.One))
//                 .Append(Validator.Create(publicKeys.Last(), BigInteger.One))
//                 .ToList();
//             Assert.Throws<ArgumentException>(() => new ImmutableSortedSet<Validator>(validators));
//         }

//         [Fact]
//         public void ZeroPowerValidatorNotAllowed()
//         {
//             List<PublicKey> publicKeys = Enumerable
//                 .Range(0, 4).Select(_ => new PrivateKey().PublicKey).ToList();
//             var zeroPowerValidator = Validator.Create(new PrivateKey().PublicKey, BigInteger.Zero);
//             var validators = publicKeys
//                 .Select(publicKey => Validator.Create(publicKey, BigInteger.One))
//                 .Append(zeroPowerValidator)
//                 .ToList();
//             Assert.Throws<ArgumentException>(() => new ImmutableSortedSet<Validator>(validators));
//         }

//         [Fact]
//         public void ValidatorsAreOrderedByAddress()
//         {
//             List<PublicKey> publicKeys = Enumerable
//                 .Range(0, 10)
//                 .Select(_ => new PrivateKey().PublicKey)
//                 .ToList();
//             ImmutableSortedSet<Validator> validatorSet = new ImmutableSortedSet<Validator>(publicKeys.Select(
//                 publicKey => Validator.Create(publicKey, BigInteger.One)).ToList());
//             TestUtils.AssertSorted(validatorSet.Validators.Select(
//                 validator => validator.OperatorAddress));
//         }

//         [Fact]
//         public void ValidatorCount()
//         {
//             List<PublicKey> publicKeys = Enumerable
//                 .Range(0, 10)
//                 .Select(_ => new PrivateKey().PublicKey)
//                 .ToList();
//             ImmutableSortedSet<Validator> validatorSet = new ImmutableSortedSet<Validator>(publicKeys.Select(
//                 publicKey => Validator.Create(publicKey, BigInteger.One)).ToList());
//             Assert.Equal(10, validatorSet.TotalCount);
//             Assert.Equal(6, validatorSet.TwoThirdsCount);
//             Assert.Equal(3, validatorSet.OneThirdCount);
//         }

//         [Fact]
//         public void ValidatorTotalPower()
//         {
//             List<PublicKey> publicKeys = Enumerable
//                 .Range(0, 10)
//                 .Select(_ => new PrivateKey().PublicKey)
//                 .ToList();
//             ImmutableSortedSet<Validator> validatorSet = new ImmutableSortedSet<Validator>(publicKeys.Select(
//                 publicKey => Validator.Create(publicKey, BigInteger.One)).ToList());
//             Assert.Equal(10, validatorSet.TotalPower);
//             Assert.Equal(6, validatorSet.TwoThirdsPower);
//             Assert.Equal(3, validatorSet.OneThirdPower);
//         }

//         [Fact]
//         public void Update()
//         {
//             List<Validator> validators = Enumerable
//                 .Range(0, 10)
//                 .Select(_ => Validator.Create(new PrivateKey().PublicKey, BigInteger.One))
//                 .ToList();
//             ImmutableSortedSet<Validator> validatorSet = new ImmutableSortedSet<Validator>(validators);

//             Assert.True(validatorSet.Validators.All(v => v.Power.Equals(BigInteger.One)));

//             // Add a new validator
//             var newValidator = Validator.Create(new PrivateKey().PublicKey, BigInteger.One);
//             var addValidatorSet = validatorSet.Update(newValidator);
//             Assert.Contains(newValidator, addValidatorSet.Validators);
//             Assert.Equal(11, addValidatorSet.Validators.Count);

//             // Modify an existing validator
//             var modValidator = Validator.Create(validators[3].PublicKey, new BigInteger(3));
//             var modValidatorSet = validatorSet.Update(modValidator);
//             Assert.Contains(modValidator, modValidatorSet.Validators);
//             Assert.Equal(10, modValidatorSet.Validators.Count);

//             // Remove a non-existing validator
//             var zeroPowerValidator = Validator.Create(new PrivateKey().PublicKey, BigInteger.Zero);
//             var noopValidatorSet = validatorSet.Update(zeroPowerValidator);
//             Assert.Equal(validatorSet, noopValidatorSet);

//             // Remove an existing validator
//             var subValidator = Validator.Create(validators[3].PublicKey, BigInteger.Zero);
//             var subValidatorSet = validatorSet.Update(subValidator);
//             Assert.DoesNotContain(subValidator.PublicKey, subValidatorSet.PublicKeys);
//             Assert.Equal(9, subValidatorSet.Validators.Count);
//         }

//         [Fact]
//         public void ValidateBlockCommitValidators()
//         {
//             Random random = new Random();
//             int height = 3;
//             int round = 5;
//             BlockHash hash = random.NextBlockHash();

//             var unorderedPrivateKeys = Enumerable
//                 .Range(0, 10)
//                 .Select(_ => new PrivateKey())
//                 .ToList();
//             var orderedPrivateKeys = unorderedPrivateKeys
//                 .OrderBy(key => key.Address)
//                 .ToList();
//             var validatorSet = new ImmutableSortedSet<Validator>(unorderedPrivateKeys.Select(
//                 key => Validator.Create(key.PublicKey, BigInteger.One)).ToList());
//             var unorderedVotes = unorderedPrivateKeys
//                 .Select(
//                     key => new VoteMetadata(
//                         height,
//                         round,
//                         hash,
//                         DateTimeOffset.UtcNow,
//                         key.PublicKey,
//                         BigInteger.One,
//                         VoteFlag.PreCommit).Sign(key))
//                 .ToImmutableArray();
//             var orderedVotes = orderedPrivateKeys
//                 .Select(
//                     key => new VoteMetadata(
//                         height,
//                         round,
//                         hash,
//                         DateTimeOffset.UtcNow,
//                         key.PublicKey,
//                         BigInteger.One,
//                         VoteFlag.PreCommit).Sign(key))
//                 .ToImmutableArray();
//             var invalidPowerVotes = orderedPrivateKeys
//                 .Select(
//                     key => new VoteMetadata(
//                         height,
//                         round,
//                         hash,
//                         DateTimeOffset.UtcNow,
//                         key.PublicKey,
//                         2,
//                         VoteFlag.PreCommit).Sign(key))
//                 .ToImmutableArray();

//             var blockCommitWithUnorderedVotes =
//                 new BlockCommit(height, round, hash, unorderedVotes);
//             var blockCommitWithInvalidPowerVotes =
//                 new BlockCommit(height, round, hash, invalidPowerVotes);
//             var blockCommitWithInsufficientVotes =
//                 new BlockCommit(height, round, hash, orderedVotes.Take(5).ToImmutableArray());
//             var validBlockCommit = new BlockCommit(height, round, hash, orderedVotes);

//             Assert.Throws<InvalidBlockCommitException>(() =>
//                 validatorSet.ValidateBlockCommitValidators(blockCommitWithUnorderedVotes));
//             Assert.Throws<InvalidBlockCommitException>(() =>
//                 validatorSet.ValidateBlockCommitValidators(blockCommitWithInvalidPowerVotes));
//             Assert.Throws<InvalidBlockCommitException>(() =>
//                 validatorSet.ValidateBlockCommitValidators(blockCommitWithInsufficientVotes));
//             validatorSet.ValidateBlockCommitValidators(validBlockCommit);
//         }
//     }
// }
