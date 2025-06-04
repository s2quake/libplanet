using static Libplanet.Types.Tests.RandomUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void ImmutableDictionaryProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableDictionary(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedObject);
        var actualObject = ModelSerializer.DeserializeFromBytes<RecordClassWithImmutableDictionary>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(
        Version = 1,
        TypeName = "Libplanet.Serialization.Tests.ModelSerializerTest+RecordClassWithImmutableDictionary")]
    public sealed record class RecordClassWithImmutableDictionary
        : IEquatable<RecordClassWithImmutableDictionary>
    {
        public RecordClassWithImmutableDictionary()
        {
        }

        public RecordClassWithImmutableDictionary(Random random)
        {
            Value1 = ImmutableDictionary(random, Int32, String);
        }

        [Property(0)]
        public ImmutableDictionary<int, string> Value1 { get; init; }
            = System.Collections.Immutable.ImmutableDictionary<int, string>.Empty;

        public bool Equals(RecordClassWithImmutableDictionary? other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }
}
