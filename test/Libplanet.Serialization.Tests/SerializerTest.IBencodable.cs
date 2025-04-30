using Bencodex.Types;
using static Libplanet.Types.BencodexUtility;

namespace Libplanet.Serialization.Tests;

public sealed partial class SerializerTest
{
    public static IEnumerable<object[]> BencodableValues =>
    [
        [new BencodableRecordClass { Value = 123 }],
        [new BencodableRecordStruct { Value = 456 }],
        [new BencodableClass { Value = 789 }],
        [new BencodableStruct { Value = 101112 }],
    ];

    [Theory]
    [InlineData(typeof(BencodableRecordClass))]
    public void CanSupport_IBencodable_Test(Type type)
    {
        Assert.True(ModelSerializer.CanSupportType(type));
    }

    [Fact]
    public void CannotSupport_IBencodableType_Test()
    {
        Assert.False(ModelSerializer.CanSupportType(typeof(IBencodable)));
    }

    [Theory]
    [MemberData(nameof(BencodexValues))]
    public void IBencodableType_SerializeAndDeserialize_Test(IValue value)
    {
        var serialized = ModelSerializer.Serialize(value);
        var actualValue = ModelSerializer.Deserialize(serialized, value.GetType())!;
        Assert.Equal(value, actualValue);
    }

    public readonly record struct BencodableRecordStruct : IBencodable
    {
        public BencodableRecordStruct(IValue value)
        {
            if (value is not List list)
            {
                throw new ArgumentException("The value is not a list.", nameof(value));
            }

            Value = ToInt32(list, 0);
        }

        public int Value { get; init; }

        public IValue Bencoded => new List(ToValue(Value));
    }

    public readonly struct BencodableStruct : IBencodable
    {
        public BencodableStruct(IValue value)
        {
            if (value is not List list)
            {
                throw new ArgumentException("The value is not a list.", nameof(value));
            }

            Value = ToInt32(list, 0);
        }

        public int Value { get; init; }

        public IValue Bencoded => new List(ToValue(Value));
    }

    public sealed record class BencodableRecordClass : IBencodable
    {
        public BencodableRecordClass()
        {
        }

        public BencodableRecordClass(IValue value)
        {
            if (value is not List list)
            {
                throw new ArgumentException("The value is not a list.", nameof(value));
            }

            Value = ToInt32(list, 0);
        }

        public int Value { get; set; }

        public IValue Bencoded => new List(ToValue(Value));
    }

    public sealed class BencodableClass : IBencodable
    {
        public BencodableClass()
        {
        }

        public BencodableClass(IValue value)
        {
            if (value is not List list)
            {
                throw new ArgumentException("The value is not a list.", nameof(value));
            }

            Value = ToInt32(list, 0);
        }

        public int Value { get; set; }

        public IValue Bencoded => new List(ToValue(Value));
    }
}
