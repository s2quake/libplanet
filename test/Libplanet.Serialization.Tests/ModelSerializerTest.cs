
using System.Diagnostics;
using System.IO;

namespace Libplanet.Serialization.Tests;

public sealed class ModelSerializerTest(ITestOutputHelper output) : ModelSerializerTestBase<byte[]>(output)
{
    protected override object? Deserialize(byte[] serialized, Type type, ModelOptions options)
        => ModelSerializer.Deserialize(serialized, type, options);

    protected override byte[] Serialize(object? obj, Type type, ModelOptions options)
        => ModelSerializer.Serialize(obj, type, options);

    [Fact]
    public void NullValue_Test()
    {
        var bytes = ModelSerializer.Serialize(obj: null, typeof(object));
        Assert.Equal([0], bytes);
    }

    [Fact]
    public void DefaultValue_OmitRootTypeInfo_Test()
    {
        var options = new ModelOptions { TypeInfoMode = ModelTypeInfoMode.Never };
        Assert.Equal([1], ModelSerializer.Serialize(0, typeof(int), options));
    }

    [Fact]
    public void DefaultValue_Test()
    {
        var bytes = ModelSerializer.Serialize(0);
        Assert.NotEqual([1], bytes);
    }

    [Fact]
    public void ZeroByte_Deserialize_ThrowTest()
    {
        byte[] bytes = [];
        Assert.Throws<OverflowException>(() => ModelSerializer.Deserialize(bytes));
    }

    [Fact]
    public void HasModelConverter_Test()
    {
        var obj1 = new HasModelConverter();
        var serialized = ModelSerializer.Serialize(obj1);
        var obj2 = ModelSerializer.Deserialize<HasModelConverter>(serialized);
        Assert.Equal(obj1, obj2);
    }

    [Fact]
    public void NotHasModelConverter_ThrowTest()
    {
        var obj1 = new NotHasModelConverter();
        var message = $"Type '{typeof(NotHasModelConverter)}' is not supported or not registered in known types.";
        var e = Assert.Throws<InvalidModelException>(() => ModelSerializer.Serialize(obj1));
        Assert.Equal(message, e.Message);
    }
}

[ModelConverter(
    typeof(HasModelConverterModelConverter),
    "Libplanet_Serialization_Tests_ModelSerializerTest_HasModelConverter")]
public sealed record class HasModelConverter
{
    public int Value { get; init; } = 123;
}

public sealed record class NotHasModelConverter
{
    public int Value { get; init; } = 123;
}

public sealed class HasModelConverterModelConverter : ModelConverter
{
    public HasModelConverterModelConverter()
        : base(typeof(HasModelConverter))
    {
    }

    protected override object Read(BinaryReader reader, Type type, ModelOptions options)
    {
        var length = sizeof(int);
        Span<byte> bytes = stackalloc byte[length];
        if (reader.Read(bytes) != length)
        {
            throw new EndOfStreamException("Failed to read the expected number of bytes.");
        }

        return new HasModelConverter
        {
            Value = BitConverter.ToInt32(bytes),
        };
    }

    protected override void Write(BinaryWriter writer, object value, ModelOptions options)
    {
        if (value is HasModelConverter instance)
        {
            writer.Write(BitConverter.GetBytes(instance.Value));
        }
        else
        {
            throw new UnreachableException("The object is not of type HasModelConverter.");
        }
    }
}
