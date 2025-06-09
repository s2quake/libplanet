using static Libplanet.Types.Tests.RandomUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void NullableProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithNullableProperty(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedObject);
        var actualObject = ModelSerializer.DeserializeFromBytes<RecordClassWithNullableProperty>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithNullableProperty")]
    public sealed record class RecordClassWithNullableProperty : IEquatable<RecordClassWithNullableProperty>
    {
        public RecordClassWithNullableProperty()
        {
        }

        public RecordClassWithNullableProperty(Random random)
        {
            Value1 = Nullable(random, Int32);
            Value2 = MaybeImmutableArray(random, Int32);
            Value3 = ImmutableArray(random, MaybeInt32);
            Value4 = MaybeImmutableArray(random, MaybeInt32);
        }

        [Property(0)]
        public int? Value1 { get; init; }

        [Property(1)]
        public ImmutableArray<int>? Value2 { get; init; } = [];

        [Property(2)]
        public ImmutableArray<int?> Value3 { get; init; } = [];

        [Property(3)]
        public ImmutableArray<int?>? Value4 { get; init; } = [];

        public bool Equals(RecordClassWithNullableProperty? other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }
}
