using System.Reflection;
using Libplanet.Serialization;
using Libplanet.TestUtilities;

namespace Libplanet.Types.Tests;

public sealed class VoteTest(ITestOutputHelper output)
{
    [Fact]
    public void Attribute()
    {
        var attribute = typeof(Vote).GetCustomAttribute<ModelAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("Vote", attribute.TypeName);
        Assert.Equal(1, attribute.Version);
    }

    [Fact]
    public void SerializeAndDeserialize()
    {
        var random = Rand.GetRandom(output);
        var value1 = Rand.Vote(random);
        var serialized = ModelSerializer.SerializeToBytes(value1);
        var value2 = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Base_Test()
    {
        var random = Rand.GetRandom(output);
        var validatorKey = Rand.PrivateKey(random);
        var validator = validatorKey.Address;
        var blockHash = Rand.BlockHash(random);
        var height = Rand.NonNegative(random);
        var round = Rand.NonNegative(random);
        var timestamp = Rand.DateTimeOffset(random);
        var validatorPower = Rand.Positive(random);
        var type = Rand.Try(
            random, Rand.Enum<VoteType>, item => item is not VoteType.Null and not VoteType.Unknown);

        var metadata = new VoteMetadata
        {
            Validator = validator,
            BlockHash = blockHash,
            Height = height,
            Round = round,
            Timestamp = timestamp,
            ValidatorPower = validatorPower,
            Type = type,
        };
        var vote = metadata.Sign(validatorKey.AsSigner());

        Assert.Equal(height, vote.Height);
        Assert.Equal(round, vote.Round);
        Assert.Equal(blockHash, vote.BlockHash);
        Assert.Equal(timestamp, vote.Timestamp);
        Assert.Equal(validator, vote.Validator);
        Assert.Equal(validatorPower, vote.ValidatorPower);
        Assert.Equal(type, vote.Type);
        Assert.Equal(metadata, vote.Metadata);
        Assert.True(ModelValidationUtility.TryValidate(vote));
    }
}
