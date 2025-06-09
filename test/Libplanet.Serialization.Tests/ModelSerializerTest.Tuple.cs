using static Libplanet.Types.Tests.RandomUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
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
        var serialized = ModelSerializer.SerializeToBytes(expectedObject);
        var actualObject = ModelSerializer.DeserializeFromBytes<RecordClassWithTuple>(serialized)!;
        Assert.Equal(expectedObject, actualObject);
    }

    [Model(Version = 1, TypeName = "Libplanet_Serialization_Tests_ModelSerializerTest_RecordClassWithTuple")]
    public sealed record class RecordClassWithTuple : IEquatable<RecordClassWithTuple>
    {
        public RecordClassWithTuple()
        {
        }

        public RecordClassWithTuple(Random random)
        {
            Value1 = Tuple(random, Int32, Boolean);
            Value2 = MaybeTuple(random, Int32, Boolean);
            Value3 = Tuple(random, MaybeInt32, MaybeBoolean);
            Value4 = MaybeTuple(random, MaybeInt32, MaybeBoolean);
            Value5 = ImmutableArray(random, random => Tuple(random, Int32, Boolean));
            Value6 = MaybeImmutableArray(random, random => Tuple(random, Int32, Boolean));
            Value7 = ImmutableArray(random, random => Tuple(random, MaybeInt32, MaybeBoolean));
            Value8 = MaybeImmutableArray(random, random => Tuple(random, MaybeInt32, MaybeBoolean));
            Value9 = ImmutableArray(random, random => MaybeTuple(random, Int32, Boolean));
            Value10 = MaybeImmutableArray(random, random => MaybeTuple(random, Int32, Boolean));
            Value11 = ImmutableArray(random, random => MaybeTuple(random, MaybeInt32, MaybeBoolean));
            Value12 = MaybeImmutableArray(random, random => MaybeTuple(random, MaybeInt32, MaybeBoolean));
        }

        [Property(0)]
        public Tuple<int, bool> Value1 { get; init; } = new(0, false);

        [Property(1)]
        public Tuple<int, bool>? Value2 { get; init; } = new(0, false);

        [Property(2)]
        public Tuple<int?, bool?> Value3 { get; init; } = new(null, null);

        [Property(3)]
        public Tuple<int?, bool?>? Value4 { get; init; } = new(null, null);

        [Property(4)]
        public ImmutableArray<Tuple<int, bool>> Value5 { get; init; } = [];

        [Property(5)]
        public ImmutableArray<Tuple<int, bool>>? Value6 { get; init; } = [];

        [Property(6)]
        public ImmutableArray<Tuple<int?, bool?>> Value7 { get; init; } = [];

        [Property(7)]
        public ImmutableArray<Tuple<int?, bool?>>? Value8 { get; init; } = [];

        [Property(8)]
        public ImmutableArray<Tuple<int, bool>?> Value9 { get; init; } = [];

        [Property(9)]
        public ImmutableArray<Tuple<int, bool>?>? Value10 { get; init; } = [];

        [Property(10)]
        public ImmutableArray<Tuple<int?, bool?>?> Value11 { get; init; } = [];

        [Property(11)]
        public ImmutableArray<Tuple<int?, bool?>?>? Value12 { get; init; } = [];

        public bool Equals(RecordClassWithTuple? other) => ModelResolver.Equals(this, other);

        public override int GetHashCode() => ModelResolver.GetHashCode(this);
    }
}
