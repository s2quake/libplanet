using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Fact]
    public void HasTypeConverter_Test()
    {
        var obj1 = new HasTypeConverter();
        var serialized = ModelSerializer.SerializeToBytes(obj1);
        var obj2 = ModelSerializer.DeserializeFromBytes<HasTypeConverter>(serialized);
        Assert.Equal(obj1, obj2);
    }

    [Fact]
    public void NotHasTypeConverter_ThrowTest()
    {
        var obj1 = new NotHasTypeConverter();
        Assert.Throws<ModelSerializationException>(() => ModelSerializer.SerializeToBytes(obj1));
    }

    [TypeConverter(typeof(HasTypeConverterTypeConverter))]
    public sealed record class HasTypeConverter
    {
        public int Value { get; init; } = 123;
    }

    public sealed record class NotHasTypeConverter
    {
        public int Value { get; init; } = 123;
    }

    private sealed class HasTypeConverterTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            if (typeof(byte[]).IsAssignableFrom(sourceType))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is byte[] integer)
            {
                return new HasTypeConverter { Value = BitConverter.ToInt32(integer, 0) };
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType)
        {
            if (destinationType == typeof(byte[]))
            {
                return true;
            }

            return base.CanConvertTo(context, destinationType);
        }

        public override object? ConvertTo(
            ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(byte[]) && value is HasTypeConverter hasTypeConverter)
            {
                return BitConverter.GetBytes(hasTypeConverter.Value);
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
