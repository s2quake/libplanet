using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Fact]
    public void HasModelConverter_Test()
    {
        var obj1 = new HasModelConverter();
        var serialized = ModelSerializer.SerializeToBytes(obj1);
        var obj2 = ModelSerializer.DeserializeFromBytes<HasModelConverter>(serialized);
        Assert.Equal(obj1, obj2);
    }

    [Fact]
    public void NotHasModelConverter_ThrowTest()
    {
        var obj1 = new NotHasModelConverter();
        Assert.Throws<ModelSerializationException>(() => ModelSerializer.SerializeToBytes(obj1));
    }

    [ModelConverter(typeof(HasModelConverterModelConverter))]
    public sealed record class HasModelConverter
    {
        public int Value { get; init; } = 123;
    }

    public sealed record class NotHasModelConverter
    {
        public int Value { get; init; } = 123;
    }

    private sealed class HasModelConverterModelConverter : ModelConverterBase
    {
        protected override object Deserialize(Stream stream, ModelOptions options)
        {
            var length = sizeof(int);
            Span<byte> bytes = stackalloc byte[length];
            if (stream.Read(bytes) != length)
            {
                throw new EndOfStreamException("Failed to read the expected number of bytes.");
            }

            return new HasModelConverter
            {
                Value = BitConverter.ToInt32(bytes),
            };
        }

        protected override void Serialize(object obj, Stream stream, ModelOptions options)
        {
            if (obj is HasModelConverter instance)
            {
                stream.Write(BitConverter.GetBytes(instance.Value));
            }
            else
            {
                throw new UnreachableException("The object is not of type HasModelConverter.");
            }
        }
    }
}