namespace Libplanet.Serialization.Tests;

public sealed partial class SerializerTest
{
    [Fact]
    public void CanSupport_LegacyModelType_FailTest()
    {
        Assert.False(ModelSerializer.CanSupportType(typeof(LegacyModelRecord1)));
    }

    [Fact]
    public void LegacyModel_SerializeAndDeserialize_Test()
    {
        var options = new ModelOptions
        {
            Resolver = new LegacyModelResolver(typeof(ModelRecord)),
        };
        var expectedObject = new LegacyModelRecord1 { Int = Random.Shared.Next() };
        var serialized = ModelSerializer.Serialize(expectedObject, options);
        var actualObject = ModelSerializer.Deserialize<ModelRecord>(serialized, options)!;
        Assert.Equal(expectedObject.Int, actualObject.Int);
        Assert.Equal("Hello, World!", actualObject.String);
    }

    public sealed record class LegacyModelRecord1
    {
        [Property(0)]
        public int Int { get; set; }
    }

    public sealed record class LegacyModelRecord2
    {
        public LegacyModelRecord2(LegacyModelRecord1 legacyModel)
        {
            Int = legacyModel.Int;
            String = "Hello, World!";
        }

        [Property(0)]
        public int Int { get; set; }

        [Property(1)]
        public string String { get; set; } = string.Empty;
    }

    [LegacyModel(Version = 1, Type = typeof(LegacyModelRecord1))]
    [LegacyModel(Version = 2, Type = typeof(LegacyModelRecord2))]
    [Model(Version = 3)]
    public sealed record class ModelRecord
    {
        public ModelRecord()
        {
        }

        public ModelRecord(LegacyModelRecord2 legacyModel)
        {
            Int = legacyModel.Int;
            String = legacyModel.String;
        }

        [Property(0)]
        public string String { get; set; } = string.Empty;

        [Property(1)]
        public int Int { get; set; }
    }
}
