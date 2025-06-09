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
    public void ImmutableSortedDictionaryProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithImmutableSortedDictionary(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedObject);
        var actualObject = ModelSerializer.DeserializeFromBytes<RecordClassWithImmutableSortedDictionary>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(
        Version = 1, 
        TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithImmutableSortedDictionary")]
    public sealed record class RecordClassWithImmutableSortedDictionary
        : IEquatable<RecordClassWithImmutableSortedDictionary>
    {
        public RecordClassWithImmutableSortedDictionary()
        {
        }

        public RecordClassWithImmutableSortedDictionary(Random random)
        {
            Value1 = ImmutableSortedDictionary(random, Int32, String);
        }

        [Property(0)]
        public ImmutableSortedDictionary<int, string> Value1 { get; init; }
            = System.Collections.Immutable.ImmutableSortedDictionary<int, string>.Empty;

        public bool Equals(RecordClassWithImmutableSortedDictionary? other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }
}
