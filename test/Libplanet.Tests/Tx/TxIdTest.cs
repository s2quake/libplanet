using Libplanet.Types.Tx;
using Xunit;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Tx;

public class TxIdTest
{
    [Fact]
    public void TxIdMustBe32Bytes()
    {
        for (var size = 0; size < 36; size++)
        {
            if (size == 32)
            {
                continue;
            }

            var bytes = GetRandomBytes(size);
            var immutableBytes = bytes.ToImmutableArray();
            Assert.Throws<ArgumentOutOfRangeException>("bytes", () => new TxId(immutableBytes));
            Assert.Throws<ArgumentOutOfRangeException>("bytes", () => new TxId(bytes));
        }
    }

    [Fact]
    public void FromHex()
    {
        TxId actual = TxId.Parse(
            "45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc");
        var expected = new TxId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x9c, 0xcc,
            ]);
        Assert.Equal(expected, actual);

        Assert.Throws<FormatException>(() => TxId.Parse("0g"));
        Assert.Throws<FormatException>(() => TxId.Parse("1"));
        Assert.Throws<FormatException>(() =>
            TxId.Parse("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9c"));
        Assert.Throws<FormatException>(() =>
            TxId.Parse("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc0"));
        Assert.Throws<FormatException>(() =>
            TxId.Parse("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc00"));
    }

    [Fact]
    public void ToByteArray()
    {
        var bytes = GetRandomBytes(TxId.Size);
        var txId = new TxId(bytes);

        Assert.Equal(bytes, [.. txId.ByteArray]);
    }

    [Fact]
    public void ToHex()
    {
        var id = new TxId(
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
    public void ToString_()
    {
        var txId = new TxId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x9c, 0xcc,
            ]);
        Assert.Equal(
            "45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc",
            txId.ToString());
    }

    [Fact]
    public void Equals_()
    {
        var sameTxId1 = new TxId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x9c, 0xcc,
            ]);
        var sameTxId2 = new TxId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
                0x9c, 0xcc,
            ]);
        var differentTxId = new TxId(
            [
                0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0x00,
                0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0x00,
                0x9c, 0x00,
            ]);

        Assert.Equal(sameTxId1, sameTxId2);
        Assert.NotEqual(sameTxId2, differentTxId);

        Assert.True(sameTxId1 == sameTxId2);
        Assert.False(sameTxId2 == differentTxId);

        Assert.False(sameTxId1 != sameTxId2);
        Assert.True(sameTxId2 != differentTxId);
    }

    [Fact]
    public void Compare()
    {
        var random = new Random();
        var buffer = new byte[32];
        var txIds = Enumerable.Repeat(0, 50).Select(_ =>
        {
            random.NextBytes(buffer);
            return new TxId(buffer);
        }).ToArray();
        for (var i = 1; i < txIds.Length; i++)
        {
            var left = txIds[i - 1];
            var right = txIds[i];
            var leftString = left.ToString().ToLower();
            var rightString = right.ToString().ToLower();
            Assert.Equal(
                Math.Min(Math.Max(left.CompareTo(right), 1), -1),
                Math.Min(Math.Max(leftString.CompareTo(rightString), 1), -1));
            Assert.Equal(
                left.CompareTo(right),
                (left as IComparable).CompareTo(right));
        }

        Assert.Throws<ArgumentException>(() => txIds[0].CompareTo(null));
        Assert.Throws<ArgumentException>(() => txIds[0].CompareTo("invalid"));
    }

    [Fact]
    public void Bencoded()
    {
        var expected = new TxId(GetRandomBytes(TxId.Size));
        var deserialized = TxId.Create(expected.ToBencodex());
        Assert.Equal(expected, deserialized);
        expected = default;
        deserialized = TxId.Create(expected.ToBencodex());
        Assert.Equal(expected, deserialized);
    }

    [SkippableFact]
    public void JsonSerialization()
    {
        TxId txid = TxId.Parse(
            "45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc");
        AssertJsonSerializable(
            txid,
            "\"45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc\"");
    }
}
