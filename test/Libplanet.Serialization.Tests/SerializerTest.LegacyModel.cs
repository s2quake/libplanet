namespace Libplanet.Serialization.Tests;

public sealed partial class SerializerTest
{
    // [Fact]
    // public void CanSupport_LegacyModelType_FailTest()
    // {
    //     Assert.False(ModelSerializer.CanSupportType(typeof(Version1_ModelRecord)));
    // }

    [Fact]
    public void LegacyModel_SerializeAndDeserialize_Test()
    {
        var expectedObject = new Version1_ModelRecord { Int = Random.Shared.Next() };
        var serialized = ModelSerializer.Serialize(expectedObject);
        var actualObject = ModelSerializer.Deserialize<ModelRecord>(serialized)!;
        Assert.Equal(expectedObject.Int, actualObject.Int);
        Assert.Equal("Hello, World!", actualObject.String);
    }

    [LegacyModel(OriginType = typeof(ModelRecord))]
    public sealed record class Version1_ModelRecord
    {
        [Property(0)]
        public int Int { get; set; }
    }

    [LegacyModel(OriginType = typeof(ModelRecord))]
    public sealed record class Version2_ModelRecord
    {
        public Version2_ModelRecord()
        {
        }

        public Version2_ModelRecord(Version1_ModelRecord legacyModel)
        {
            Int = legacyModel.Int;
            String = "Hello, World!";
        }

        [Property(0)]
        public int Int { get; set; }

        [Property(1)]
        public string String { get; set; } = string.Empty;
    }

    [Model(Version = 1, Type = typeof(Version1_ModelRecord))]
    [Model(Version = 2, Type = typeof(Version2_ModelRecord))]
    [Model(Version = 3)]
    public sealed record class ModelRecord
    {
        public ModelRecord()
        {
        }

        public ModelRecord(Version2_ModelRecord legacyModel)
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
