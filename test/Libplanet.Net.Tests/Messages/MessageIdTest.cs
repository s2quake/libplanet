using Libplanet.TestUtilities;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Messages;

public sealed class MessageIdTest(ITestOutputHelper output)
{
    [Fact]
    public void MessageIdMustBe32Bytes()
    {
        var random = RandomUtility.GetRandom(output);
        for (var size = 0; size < 36; size++)
        {
            if (size == 32)
            {
                continue;
            }

            byte[] bytes = RandomUtility.Array(random, RandomUtility.Byte, size);
            Assert.Throws<ArgumentOutOfRangeException>("bytes", () => new MessageId(bytes));
        }
    }

    [Fact]
    public void Parse()
    {
        MessageId actual = MessageId.Parse(
            "45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc");
        var expected = new MessageId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x9c, 0xcc,
            ]);
        Assert.Equal(expected, actual);

        Assert.Throws<FormatException>(() => MessageId.Parse("0g"));
        Assert.Throws<FormatException>(() => MessageId.Parse("1"));
        Assert.Throws<FormatException>(
            () => MessageId.Parse("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9c"));
        Assert.Throws<FormatException>(
            () => MessageId.Parse("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc0"));
        Assert.Throws<FormatException>(
            () => MessageId.Parse("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc00"));
    }

    [Fact]
    public void ToByteArray()
    {
        var random = RandomUtility.GetRandom(output);
        var bytes = RandomUtility.Array(random, RandomUtility.Byte, MessageId.Size);
        var messageId = new MessageId(bytes);

        Assert.Equal(bytes, messageId.Bytes);
    }

    [Fact]
    public void ToString_Test()
    {
        var id = new MessageId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x9c, 0xcc,
            ]);
        Assert.Equal(
            "45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc",
            id.ToString());
    }

    [Fact]
    public void Equals_Test()
    {
        var sameId1 = new MessageId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x9c, 0xcc,
            ]);
        var sameId2 = new MessageId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x9c, 0xcc,
            ]);
        var differentId = new MessageId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0x00,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0x00,
                0x9c, 0x00,
            ]);

        Assert.Equal(sameId1, sameId2);
        Assert.NotEqual(sameId2, differentId);

        Assert.True(sameId1 == sameId2);
        Assert.False(sameId2 == differentId);

        Assert.False(sameId1 != sameId2);
        Assert.True(sameId2 != differentId);
    }

    [Fact]
    public void Compare()
    {
        var random = RandomUtility.GetRandom(output);
        var ids = RandomUtility.Array(random, RandomUtility.MessageId, 50);
        for (var i = 1; i < ids.Length; i++)
        {
            var left = ids[i - 1];
            var right = ids[i];
            var leftString = left.ToString().ToLower();
            var rightString = right.ToString().ToLower();
            Assert.Equal(
                Math.Min(Math.Max(left.CompareTo(right), 1), -1),
                Math.Min(Math.Max(leftString.CompareTo(rightString), 1), -1));
            Assert.Equal(
                left.CompareTo(right),
                (left as IComparable).CompareTo(right));
        }

        Assert.Equal(1, ids[0].CompareTo(null));
        Assert.Throws<ArgumentException>(() => ids[0].CompareTo("invalid"));
    }
}
