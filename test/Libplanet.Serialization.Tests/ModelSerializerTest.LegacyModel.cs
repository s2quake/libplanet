namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Fact]
    public void LegacyModel_SerializeAndDeserialize_Test()
    {
        var expectedObject = new Version1_ModelRecord { Int = Random.Shared.Next() };
        var serialized = ModelSerializer.SerializeToBytes(expectedObject);
        var actualObject = ModelSerializer.DeserializeFromBytes<ModelRecord>(serialized)!;
        Assert.Equal(expectedObject.Int, actualObject.Int);
        Assert.Equal("Hello, World!", actualObject.String);
    }

    [OriginModel(Type = typeof(ModelRecord), AllowSerialization = true)]
    public sealed record class Version1_ModelRecord
    {
        [Property(0)]
        public int Int { get; set; }
    }

    [OriginModel(Type = typeof(ModelRecord))]
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

    [ModelHistory(Version = 1, Type = typeof(Version1_ModelRecord))]
    [ModelHistory(Version = 2, Type = typeof(Version2_ModelRecord))]
    [Model(Version = 3, TypeName = "ModelSerializerTest+ModelRecord")]
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
