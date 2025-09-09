using GraphQL.Language.AST;
using Libplanet.Explorer.GraphTypes;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Explorer.Tests.GraphTypes
{
    public class BlockHashTypeTest : ScalarGraphTypeTestBase<BlockHashType>
    {
        [Fact]
        public void ParseLiteral()
        {
            Assert.Null(_type.ParseLiteral(new NullValue()));

            var bytes = Rand.Bytes(BlockHash.Size);
            var blockHash = new BlockHash(bytes);
            var hex = ByteUtility.Hex(bytes);
            Assert.Equal(
                blockHash,
                Assert.IsType<BlockHash>(_type.ParseLiteral(new StringValue(hex))));

            Assert.Throws<InvalidOperationException>(
                () => _type.ParseLiteral(new LongValue(1234)));
            Assert.Throws<InvalidOperationException>(
                () => _type.ParseValue(new StringValue("blockHash")));
        }

        [Fact]
        public void ParseValue()
        {
            Assert.Null(_type.ParseValue(null));

            var bytes = Rand.Bytes(BlockHash.Size);
            var blockHash = new BlockHash(bytes);
            var hex = ByteUtility.Hex(bytes);
            Assert.Equal(blockHash, _type.ParseValue(hex));

            Assert.Throws<InvalidOperationException>(() => _type.ParseValue(0));
            Assert.Throws<InvalidOperationException>(() => _type.ParseValue(new BlockHash()));
            Assert.Throws<InvalidOperationException>(() => _type.ParseValue(new object()));
        }

        [Fact]
        public void Serialize()
        {
            var bytes = Rand.Bytes(BlockHash.Size);
            var blockHash = new BlockHash(bytes);
            var hex = ByteUtility.Hex(bytes);
            Assert.Equal(hex, _type.Serialize(blockHash));

            Assert.Throws<InvalidOperationException>(() => _type.Serialize(0));
            Assert.Throws<InvalidOperationException>(() => _type.Serialize(""));
            Assert.Throws<InvalidOperationException>(() => _type.Serialize(new object()));
        }
    }
}
