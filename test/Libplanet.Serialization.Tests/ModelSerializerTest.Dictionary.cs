using static Libplanet.Tests.RandomUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void DictionaryProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithDictionary(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedObject);
        var actualObject = ModelSerializer.DeserializeFromBytes<RecordClassWithDictionary>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(Version = 1)]
    public sealed record class RecordClassWithDictionary
        : IEquatable<RecordClassWithDictionary>
    {
        public RecordClassWithDictionary()
        {
        }

        public RecordClassWithDictionary(Random random)
        {
            Value1 = Dictionary(random, Int32, String);
        }

        [Property(0)]
        public Dictionary<int, string> Value1 { get; init; } = [];

        public bool Equals(RecordClassWithDictionary? other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }
}
