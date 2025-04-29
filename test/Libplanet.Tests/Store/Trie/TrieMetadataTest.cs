using Bencodex.Types;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;

namespace Libplanet.Tests.Store.Trie
{
    public class TrieMetadataTest
    {
        [Fact]
        public void CannotCreateWithInvalidVersion()
        {
            Assert.Throws<ArgumentException>(
                () => new TrieMetadata(BlockMetadata.CurrentProtocolVersion + 1));
        }

        [Fact]
        public void Bencoded()
        {
            var meta = new TrieMetadata(BlockMetadata.CurrentProtocolVersion);
            IValue bencoded = meta.Bencoded;
            var decoded = new TrieMetadata(bencoded);
            Assert.Equal(meta.Version, decoded.Version);
        }
    }
}
