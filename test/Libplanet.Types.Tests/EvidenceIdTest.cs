using System.Text.Json;
using Libplanet.Serialization;
using Libplanet.TestUtilities;

namespace Libplanet.Types.Tests;

public sealed class EvidenceIdTest
{
    public static readonly TheoryData<byte[]> RandomBytes =
    [
        RandomUtility.Array(RandomUtility.Byte, 0),
        RandomUtility.Array(RandomUtility.Byte, 31),
        RandomUtility.Array(RandomUtility.Byte, 33),
    ];

    [Theory]
    [MemberData(nameof(RandomBytes))]
    public void Create_WithBytesLengthNot32_FailTest(byte[] evidenceId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            paramName: "bytes",
            testCode: () => new EvidenceId(evidenceId));
    }

    [Theory]
    [MemberData(nameof(RandomBytes))]
    public void Create_WithImmutableBytesLengthNot32_FailTest(byte[] evidenceId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            paramName: "bytes",
            testCode: () => new EvidenceId(evidenceId.ToImmutableArray()));
    }

    [Fact]
    public void FromHex_Test()
    {
        // Given
        var expectedEvidenceId = new EvidenceId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xcc,
        ]);

        // Then
        var actualEvidenceId = EvidenceId.Parse("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc");
        Assert.Equal(expectedEvidenceId, actualEvidenceId);
    }

    [Theory]
    [InlineData("0g")]
    public void FromHex_WithInvalidFormat_FailTest(string hex)
    {
        Assert.Throws<FormatException>(() => EvidenceId.Parse(hex));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9c")]
    [InlineData("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc0")]
    [InlineData("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc00")]
    public void FromHex_WithInvalidLength_FailTest(string hex)
    {
        Assert.Throws<FormatException>(() => EvidenceId.Parse(hex));
    }

    [Fact]
    public void ToByteArray_Test()
    {
        // Given
        var expectedBytes = RandomUtility.Array(RandomUtility.Byte, EvidenceId.Size);

        // When
        var evidenceId = new EvidenceId(expectedBytes);

        // Then
        var actualBytes = evidenceId.Bytes.ToArray();

        Assert.Equal(expectedBytes, actualBytes);
    }

    [Fact]
    public void ToHex_Test()
    {
        // Given
        var evidenceId = new EvidenceId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xcc,
        ]);

        // Then
        var expectedHex = "45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc";
        var actualHex = evidenceId.ToString();
        Assert.Equal(expectedHex, actualHex);
    }

    [Fact]
    public void ToString_Test()
    {
        // Given
        var evidenceId = new EvidenceId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xcc,
        ]);

        // Then
        var expectedString = "45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc";
        var actualString = evidenceId.ToString();
        Assert.Equal(expectedString, actualString);
    }

    [Fact]
    public void Equals_Test()
    {
        // Given
        var sameEvidenceId1 = new EvidenceId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xcc,
        ]);
        var sameEvidenceId2 = new EvidenceId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0xcc,
            0x9c, 0xcc,
        ]);
        var differentEvidenceId = new EvidenceId(
        [
            0x45, 0xa2, 0x21, 0x87, 0xe2, 0xd8, 0x85, 0x0b, 0xb3, 0x57,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0x00,
            0x88, 0x69, 0x58, 0xbc, 0x3e, 0x85, 0x60, 0x92, 0x9c, 0x00,
            0x9c, 0x00,
        ]);

        // Then
        Assert.Equal(sameEvidenceId1, sameEvidenceId2);
        Assert.NotEqual(sameEvidenceId2, differentEvidenceId);

        Assert.True(sameEvidenceId1 == sameEvidenceId2);
        Assert.False(sameEvidenceId2 == differentEvidenceId);

        Assert.False(sameEvidenceId1 != sameEvidenceId2);
        Assert.True(sameEvidenceId2 != differentEvidenceId);
    }

    [Fact]
    public void Compare_Test()
    {
        const int length = 50;
        var evidenceIds = RandomUtility.Array(RandomUtility.EvidenceId, length);

        for (var i = 1; i < evidenceIds.Length; i++)
        {
            var left = evidenceIds[i - 1];
            var right = evidenceIds[i];
            var leftString = left.ToString().ToLower();
            var rightString = right.ToString().ToLower();
            Assert.Equal(
                Math.Min(Math.Max(left.CompareTo(right), 1), -1),
                Math.Min(Math.Max(leftString.CompareTo(rightString), 1), -1));
            Assert.Equal(
                left.CompareTo(right),
                (left as IComparable).CompareTo(right));
        }
    }

    [Fact]
    public void Compare_WithNull_Test()
    {
        var evidenceId = RandomUtility.EvidenceId();
        Assert.Equal(1, evidenceId.CompareTo(null));
    }

    [Fact]
    public void Compare_WithOtherType_Test()
    {
        var evidenceId = RandomUtility.EvidenceId();
        Assert.Throws<ArgumentException>(() => evidenceId.CompareTo("string"));
    }

    [Fact]
    public void Bencoded_Test()
    {
        var expectedEvidenceId = RandomUtility.EvidenceId();
        var actualEvidenceId = ModelSerializer.Clone(expectedEvidenceId);
        Assert.Equal(expectedEvidenceId, actualEvidenceId);
    }

    [Fact]
    public void Bencoded_WithDefaultInstance_Test()
    {
        var expectedEvidenceId = default(EvidenceId);
        var actualEvidenceId = ModelSerializer.Clone(expectedEvidenceId);
        Assert.Equal(expectedEvidenceId, actualEvidenceId);
    }

    [Fact]
    public void JsonSerialization()
    {
        var evidenceId = EvidenceId.Parse("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc");
        var json1 = JsonSerializer.Serialize(evidenceId);
        var json2 = JsonSerializer.Serialize("45a22187e2d8850bb357886958bc3e8560929ccc886958bc3e8560929ccc9ccc");
        Assert.Equal(json1, json2);
    }
}
