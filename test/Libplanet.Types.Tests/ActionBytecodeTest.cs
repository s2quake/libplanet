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
        var random = RandomUtility.GetRandom(output);
        var code1 = RandomUtility.ActionBytecode(random);
        var serialized = ModelSerializer.SerializeToBytes(code1);
        var code2 = ModelSerializer.DeserializeFromBytes(serialized);
        Assert.Equal(code1, code2);
    }

    [Fact]
    public void Ctor()
    {
        var random = RandomUtility.GetRandom(output);
        var bytes = RandomUtility.Bytes(random);

        Assert.Equal(bytes, new ActionBytecode(bytes).Bytes);
        Assert.Equal(bytes, new ActionBytecode(bytes.ToImmutableArray()).Bytes);
    }

    [Fact]
    public void Equals_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var bytecode1 = RandomUtility.Try(random, RandomUtility.ActionBytecode, e => e.Bytes.Length > 0);
        var bytecode2 = RandomUtility.Try(random, RandomUtility.ActionBytecode, e => e.Bytes.Length > 0);
        var sameActionBytecode = new ActionBytecode(bytecode1.Bytes);

        Assert.True(bytecode1.Equals(sameActionBytecode));
        Assert.False(bytecode1.Equals(bytecode2));
        Assert.False(bytecode1.Equals(null));
        Assert.False(bytecode1.Equals("not an address"));
    }

    [Fact]
    public void GetHashCode_Test()
    {
        var random = RandomUtility.GetRandom(output);
        var address1 = RandomUtility.ActionBytecode(random);
        var address2 = RandomUtility.ActionBytecode(random);
        var sameActionBytecode = new ActionBytecode(address1.Bytes);

        Assert.Equal(address1.GetHashCode(), ByteUtility.GetHashCode(address1.Bytes));
        Assert.Equal(address1.GetHashCode(), sameActionBytecode.GetHashCode());
        Assert.NotEqual(address1.GetHashCode(), address2.GetHashCode());
    }
}
