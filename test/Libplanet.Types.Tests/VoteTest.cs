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
        var random = RandomUtility.GetRandom(output);
        var value1 = RandomUtility.Vote(random);
        var serialized = ModelSerializer.SerializeToBytes(value1);
        var value2 = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(value1, value2);
    }

    [Fact]
    public void Base_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var validatorKey = RandomUtility.PrivateKey(random);
        var validator = validatorKey.Address;
        var blockHash = RandomUtility.BlockHash(random);
        var height = RandomUtility.NonNegative(random);
        var round = RandomUtility.NonNegative(random);
        var timestamp = RandomUtility.DateTimeOffset(random);
        var validatorPower = RandomUtility.Positive(random);
        var type = RandomUtility.Try(
            random, RandomUtility.Enum<VoteType>, item => item is not VoteType.Null and not VoteType.Unknown);

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
