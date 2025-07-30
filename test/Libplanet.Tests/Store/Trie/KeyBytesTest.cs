using Libplanet.Store.Trie;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Store.Trie;

public sealed class KeyBytesTest
{
    [Fact]
    public void Constructors()
    {
        AssertBytesEqual(ImmutableArray<byte>.Empty, default(KeyBytes).Bytes);
        AssertBytesEqual(
            ImmutableArray<byte>.Empty,
            ((KeyBytes)string.Empty).Bytes);
        AssertBytesEqual(
            ImmutableArray<byte>.Empty.Add(1).Add(2).Add(3).Add(4),
            new KeyBytes(ImmutableArray<byte>.Empty.Add(1).Add(2).Add(3).Add(4)).Bytes);
        AssertBytesEqual(
            new KeyBytes([1, 2, 3, 4, 5]).Bytes,
            KeyBytes.Create([1, 2, 3, 4, 5]).Bytes);
        AssertBytesEqual(
            new KeyBytes([0x66, 0x6f, 0x6f]).Bytes,
            ((KeyBytes)"foo").Bytes);
    }

    [Fact]
    public void Length()
    {
        Assert.Equal(0, default(KeyBytes).Length);
        Assert.Equal(2, KeyBytes.Create([0, 0]).Length);
        Assert.Equal(4, KeyBytes.Create([1, 2, 3, 4]).Length);
        Assert.Equal(5, KeyBytes.Create([1, 2, 3, 4, 5]).Length);
    }

    [Fact]
    public void ByteArray()
    {
        KeyBytes empty = default;
        AssertBytesEqual(ImmutableArray<byte>.Empty, empty.Bytes);
        AssertBytesEqual(Array.Empty<byte>(), empty.ToByteArray());

        var foo = KeyBytes.Create([0x66, 0x6f, 0x6f]);
        AssertBytesEqual(ImmutableArray.Create<byte>(0x66, 0x6f, 0x6f), foo.Bytes);
        AssertBytesEqual(new byte[] { 0x66, 0x6f, 0x6f }, foo.ToByteArray());
    }

    [Fact]
    public void Parse()
    {
        Assert.Equal(default, KeyBytes.Parse(string.Empty));
        Assert.Equal(KeyBytes.Create([1, 2, 3]), KeyBytes.Parse("010203"));
        Assert.Equal(KeyBytes.Create([0xab, 0xcd, 0xef]), KeyBytes.Parse("AbcdeF"));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyBytes.Parse("abc"));
        Assert.Throws<FormatException>(() => KeyBytes.Parse("zzzz"));
        Assert.Throws<FormatException>(() => KeyBytes.Parse("0xabcd"));
    }

    [Fact]
    public void Hex()
    {
        KeyBytes empty = default;
        var b123 = KeyBytes.Create([1, 2, 3]);
        var b122 = KeyBytes.Create([1, 2, 2]);
        var b1234 = KeyBytes.Create([1, 2, 3, 4]);

        Assert.Empty($"{empty:h}");
        Assert.Equal("010203", $"{b123:h}");
        Assert.Equal("010202", $"{b122:h}");
        Assert.Equal("01020304", $"{b1234:h}");
    }

    [Fact]
    public void Equality()
    {
        KeyBytes empty = default;
        var b123 = KeyBytes.Create([1, 2, 3]);
        var b122 = KeyBytes.Create([1, 2, 2]);
        var b1234 = KeyBytes.Create([1, 2, 3, 4]);

        Assert.True(empty.Equals(KeyBytes.Create([])));
        Assert.False(empty.Equals(b123));
        Assert.False(empty.Equals(b122));
        Assert.False(empty.Equals(b1234));
        Assert.True(empty == KeyBytes.Create([]));
        Assert.False(empty == b123);
        Assert.False(empty == b122);
        Assert.False(empty == b1234);
        Assert.False(empty != KeyBytes.Create([]));
        Assert.True(empty != b123);
        Assert.True(empty != b122);
        Assert.True(empty != b1234);
        Assert.False(empty.Equals((object)Array.Empty<byte>()));
        Assert.True(empty.Equals((object)KeyBytes.Create([])));
        Assert.False(empty.Equals((object)b123));
        Assert.False(empty.Equals((object)b122));
        Assert.False(empty.Equals((object)b1234));
        Assert.Equal(empty.GetHashCode(), KeyBytes.Create([]).GetHashCode());
        Assert.NotEqual(empty.GetHashCode(), b123.GetHashCode());
        Assert.NotEqual(empty.GetHashCode(), b122.GetHashCode());
        Assert.NotEqual(empty.GetHashCode(), b1234.GetHashCode());

        Assert.False(b123.Equals(empty));
        Assert.True(b123.Equals(KeyBytes.Create([1, 2, 3])));
        Assert.False(b123.Equals(b122));
        Assert.False(b123.Equals(b1234));
        Assert.False(b123 == KeyBytes.Create([]));
        Assert.True(b123 == KeyBytes.Create([1, 2, 3]));
        Assert.False(b123 == b122);
        Assert.False(b123 == b1234);
        Assert.True(b123 != default);
        Assert.False(b123 != KeyBytes.Create([1, 2, 3]));
        Assert.True(b123 != b122);
        Assert.True(b123 != b1234);
        Assert.False(b123.Equals((object)default(KeyBytes)));
        Assert.True(b123.Equals((object)b123));
        Assert.False(b123.Equals((object)b122));
        Assert.False(b123.Equals((object)b1234));
        Assert.NotEqual(b123.GetHashCode(), default(KeyBytes).GetHashCode());
        Assert.Equal(b123.GetHashCode(), KeyBytes.Create([1, 2, 3]).GetHashCode());
        Assert.NotEqual(b123.GetHashCode(), b122.GetHashCode());
        Assert.NotEqual(b123.GetHashCode(), b1234.GetHashCode());
    }

    [Fact]
    public void String()
    {
        KeyBytes empty = default;
        var b123 = KeyBytes.Create([1, 2, 3]);
        var b122 = KeyBytes.Create([1, 2, 2]);
        var b1234 = KeyBytes.Create([1, 2, 3, 4]);

        Assert.Equal("KeyBytes (0 B)", empty.ToString());
        Assert.Equal("KeyBytes (3 B) 010203", b123.ToString());
        Assert.Equal("KeyBytes (3 B) 010202", b122.ToString());
        Assert.Equal("KeyBytes (4 B) 01020304", b1234.ToString());
    }
}
