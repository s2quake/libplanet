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
    public void ValueTupleProperty_SerializeAndDeserialize_Test(int seed)
    {
        var random = new Random(seed);
        var expectedObject = new RecordClassWithValueTuple(random);
        var serialized = ModelSerializer.SerializeToBytes(expectedObject);
        var actualObject = ModelSerializer.DeserializeFromBytes<RecordClassWithValueTuple>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithValueTuple")]
    public sealed record class RecordClassWithValueTuple : IEquatable<RecordClassWithValueTuple>
    {
        public RecordClassWithValueTuple()
        {
        }

        public RecordClassWithValueTuple(Random random)
        {
            Value1 = ValueTuple(random, Int32, Boolean);
            Value2 = MaybeValueTuple(random, Int32, Boolean);
            Value3 = ValueTuple(random, MaybeInt32, MaybeBoolean);
            Value4 = MaybeValueTuple(random, MaybeInt32, MaybeBoolean);
            Value5 = ImmutableArray(random, random => ValueTuple(random, Int32, Boolean));
            Value6 = MaybeImmutableArray(random, random => ValueTuple(random, Int32, Boolean));
            Value7 = ImmutableArray(random, random => ValueTuple(random, MaybeInt32, MaybeBoolean));
            Value8 = MaybeImmutableArray(random, random => ValueTuple(random, MaybeInt32, MaybeBoolean));
            Value9 = ImmutableArray(random, random => MaybeValueTuple(random, Int32, Boolean));
            Value10 = MaybeImmutableArray(random, random => MaybeValueTuple(random, Int32, Boolean));
            Value11 = ImmutableArray(random, random => MaybeValueTuple(random, MaybeInt32, MaybeBoolean));
            Value12 = MaybeImmutableArray(random, random => MaybeValueTuple(random, MaybeInt32, MaybeBoolean));
        }

        [Property(0)]
        public (int, bool) Value1 { get; init; }

        [Property(1)]
        public (int, bool)? Value2 { get; init; }

        [Property(2)]
        public (int?, bool?) Value3 { get; init; }

        [Property(3)]
        public (int?, bool?)? Value4 { get; init; }

        [Property(4)]
        public ImmutableArray<(int, bool)> Value5 { get; init; } = [];

        [Property(5)]
        public ImmutableArray<(int, bool)>? Value6 { get; init; } = [];

        [Property(6)]
        public ImmutableArray<(int?, bool?)> Value7 { get; init; } = [];

        [Property(7)]
        public ImmutableArray<(int?, bool?)>? Value8 { get; init; } = [];

        [Property(8)]
        public ImmutableArray<(int, bool)?> Value9 { get; init; } = [];

        [Property(9)]
        public ImmutableArray<(int, bool)?>? Value10 { get; init; } = [];

        [Property(10)]
        public ImmutableArray<(int?, bool?)?> Value11 { get; init; } = [];

        [Property(11)]
        public ImmutableArray<(int?, bool?)?>? Value12 { get; init; } = [];

        public bool Equals(RecordClassWithValueTuple? other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }
}
