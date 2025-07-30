using GraphQL.Language.AST;
using Libplanet.Explorer.GraphTypes;

namespace Libplanet.Explorer.Tests.GraphTypes
{
    public class ByteStringTypeTest : ScalarGraphTypeTestBase<ByteStringType>
    {
        [Theory]
        [InlineData(new byte[0], "")]
        [InlineData(new byte[] { 0xbe, 0xef }, "beef")]
        [InlineData("foo", "foo")]
        [InlineData(1, null)]
        [InlineData(null, null)]
        public void Serialize(object value, string expected)
        {
            Assert.Equal(expected, _type.Serialize(value));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("beef", new byte[] { 0xbe, 0xef })]
        public void ParseValue(object value, object parsed)
        {
            Assert.Equal(parsed, _type.ParseValue(value));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("beef", new byte[] { 0xbe, 0xef })]
        public void ParseLiteral(string stringValue, object parsed)
        {
            var actual =
                stringValue is { } v ? _type.ParseLiteral(new StringValue(v)) : null;
            Assert.Equal(parsed, actual);
        }

        [Fact]
        public void ParseLiteral_NotStringValue_ReturnNull()
        {
            Assert.Null(_type.ParseLiteral(new IntValue(0)));
            Assert.Null(_type.ParseLiteral(new BigIntValue(0)));
            Assert.Null(_type.ParseLiteral(new EnumValue("NAME")));
        }
    }
}
