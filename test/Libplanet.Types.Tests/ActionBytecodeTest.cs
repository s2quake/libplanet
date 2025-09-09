using System.Reflection;
using Libplanet.Serialization;
using Libplanet.TestUtilities;

namespace Libplanet.Types.Tests;

public sealed partial class ActionBytecodeTest(ITestOutputHelper output)
{
    [Fact]
    public void Attribute()
    {
        var attribute = typeof(ActionBytecode).GetCustomAttribute<ModelConverterAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("action", attribute.TypeName);
    }

    [Fact]
    public void SerializeAndDeserialize()
    {
        var random = Rand.GetRandom(output);
        var code1 = Rand.ActionBytecode(random);
        var serialized = ModelSerializer.SerializeToBytes(code1);
        var code2 = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(code1, code2);
    }

    [Fact]
    public void Ctor()
    {
        var random = Rand.GetRandom(output);
        var bytes = Rand.Bytes(random);

        Assert.Equal(bytes, new ActionBytecode(bytes).Bytes);
        Assert.Equal(bytes, new ActionBytecode(bytes.ToImmutableArray()).Bytes);
    }

    [Fact]
    public void Equals_Test()
    {
        var random = Rand.GetRandom(output);
        var bytecode1 = Rand.Try(random, Rand.ActionBytecode, e => e.Bytes.Length > 0);
        var bytecode2 = Rand.Try(random, Rand.ActionBytecode, e => e.Bytes.Length > 0);
        var sameActionBytecode = new ActionBytecode(bytecode1.Bytes);

        Assert.True(bytecode1.Equals(sameActionBytecode));
        Assert.False(bytecode1.Equals(bytecode2));
        Assert.False(bytecode1.Equals(null));
        Assert.False(bytecode1.Equals("not an address"));
        Assert.Equal(default, new ActionBytecode(default(ImmutableArray<byte>)));
    }

    [Fact]
    public void GetHashCode_Test()
    {
        var random = Rand.GetRandom(output);
        var address1 = Rand.ActionBytecode(random);
        var address2 = Rand.ActionBytecode(random);
        var sameActionBytecode = new ActionBytecode(address1.Bytes);
        var defaultAddress1 = default(ActionBytecode);
        var defaultAddress2 = new ActionBytecode(default(ImmutableArray<byte>));

        Assert.Equal(address1.GetHashCode(), ByteUtility.GetHashCode(address1.Bytes));
        Assert.Equal(address1.GetHashCode(), sameActionBytecode.GetHashCode());
        Assert.NotEqual(address1.GetHashCode(), address2.GetHashCode());
        Assert.Equal(0, defaultAddress1.GetHashCode());
        Assert.Equal(0, defaultAddress2.GetHashCode());
    }
}
