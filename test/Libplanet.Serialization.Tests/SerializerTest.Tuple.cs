using Bencodex.Types;
using static Libplanet.Tests.RandomUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class SerializerTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [MemberData(nameof(RandomSeeds))]
    public void TupleProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithTuple(random);
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<RecordClassWithTuple>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(Version = 1)]
    public sealed record class RecordClassWithTuple : IEquatable<RecordClassWithTuple>
    {
        public RecordClassWithTuple()
        {
        }

        public RecordClassWithTuple(Random random)
        {
            Value1 = ValueTuple(random, Int32, Boolean);
            Value2 = ValueTuple(random, MaybeInt32, MaybeBoolean);
            Value3 = MaybeValueTuple(random, MaybeInt32, MaybeBoolean);
            // Value2 = NullableObject(random, random => ImmutableArray(random, Int32));
            // Value3 = ImmutableArray(random, random => Nullable(random, Int32));
            // Value4 = NullableObject(random, random => ImmutableArray(random, random => Nullable(random, Int32)));
        }

        [Property(0)]
        public (int, bool) Value1 { get; init; }

        [Property(1)]
        public (int?, bool?) Value2 { get; init; }

        [Property(1)]
        public (int?, bool?)? Value3 { get; init; }

        // [Property(1)]
        // public ImmutableArray<int>? Value2 { get; init; } = [];

        // [Property(2)]
        // public ImmutableArray<int?> Value3 { get; init; } = [];

        // [Property(3)]
        // public ImmutableArray<int?>? Value4 { get; init; } = [];

        public bool Equals(RecordClassWithTuple? other) => ModelUtility.Equals(this, other);

        public override int GetHashCode() => ModelUtility.GetHashCode(this);
    }
}
