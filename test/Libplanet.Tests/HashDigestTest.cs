using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types.Tests;

namespace Libplanet.Tests;

public partial class HashDigestTest
{
    [Theory]
    [InlineData(0)]
    [ClassData(typeof(RandomSeedsData))]
    public void SerializeAndDeserialize_SHA256(int seed)
    {
        var random = new Random(seed);
        var expectedValue = RandomUtility.HashDigest<SHA256>(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedValue);
        var actualValue = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(expectedValue, actualValue);
    }
}
