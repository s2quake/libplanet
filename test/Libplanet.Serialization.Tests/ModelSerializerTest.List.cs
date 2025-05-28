#pragma warning disable SA1414 // Tuple types in signatures should have element names
using static Libplanet.Types.Tests.RandomUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void ListProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithList(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedObject);
        var actualObject = ModelSerializer.DeserializeFromBytes<RecordClassWithList>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(Version = 1)]
    public sealed record class RecordClassWithList
        : IEquatable<RecordClassWithList>
    {
        public RecordClassWithList()
        {
        }

        public RecordClassWithList(Random random)
        {
            Value1 = List(random, Int32);
        }

        [Property(0)]
        public List<int> Value1 { get; init; } = [];

        public bool Equals(RecordClassWithList? other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }
}
