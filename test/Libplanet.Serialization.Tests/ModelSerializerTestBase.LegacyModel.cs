namespace Libplanet.Serialization.Tests;

public abstract partial class ModelSerializerTestBase<TData>
{
    [Fact]
    public void LegacyModel_SerializeAndDeserialize_Test()
    {
        var expectedObject = new Version1_ModelRecord { Int = Random.Shared.Next() };
        var serialized = Serialize(expectedObject);
        var actualObject = Deserialize<ModelRecord>(serialized)!;
        Assert.Equal(expectedObject.Int, actualObject.Int);
        Assert.Equal("Hello, World!", actualObject.String);
    }
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
[Model("Libplanet_Serialization_Tests_ModelRecord", Version = 3)]
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
