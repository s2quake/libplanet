#pragma warning disable SA1414 // Tuple types in signatures should have element names
using Libplanet.TestUtilities;

namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1074183504)]
    [InlineData(1849913649)]
    [ClassData(typeof(RandomSeedsData))]
    public void SerializeAndDeserialize_Parallels_Test(int seed)
    {
        var random = new Random(seed);
        object[] expectedObjects =
        [
            new RecordClassWithArray(random),
            new RecordClassWithValueTuple(random),
            new ObjectStruct(random),
            new ArrayStruct(random),
            new MixedStruct(random),
            new ObjectClass(random),
            new ArrayClass(random),
            new MixedClass(random),
            new RecordClassWithImmutableDictionary(random),
            new RecordClassWithImmutableSortedDictionary(random),
            new RecordClassWithNullableProperty(random),
        ];
        Parallel.ForEach(
            expectedObjects,
            expectedObject =>
            {
                var serialized = Serialize(expectedObject);
                var actualObject = Deserialize(serialized) ?? throw new InvalidOperationException();
                Assert.Equal(expectedObject, actualObject);
            });
    }
}
