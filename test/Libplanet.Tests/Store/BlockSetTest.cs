using Libplanet.Store;
using Libplanet.Types.Blocks;

namespace Libplanet.Tests.Store
{
    public class BlockSetTest : IDisposable
    {
        private readonly StoreFixture _fx;
        private readonly BlockStore _set;

        public BlockSetTest()
        {
            _fx = new MemoryStoreFixture();
            _set = new BlockStore(_fx.Store, Guid.NewGuid());
        }

        [Fact]
        public void CanStoreItem()
        {
            _set[_fx.Block1.BlockHash] = _fx.Block1;
            Assert.Equal(_set[_fx.Block1.BlockHash], _fx.Block1);
        }

        [Fact]
        public void CanIterateItems()
        {
            Assert.Empty(_set);
            Assert.Empty(_set.Keys);
            Assert.Empty(_set.Values);
            _set[_fx.Block1.BlockHash] = _fx.Block1;
            _set[_fx.Block2.BlockHash] = _fx.Block2;
            _set[_fx.Block3.BlockHash] = _fx.Block3;

            Assert.Equal(
                new HashSet<BlockHash>()
                {
                    _fx.Block1.BlockHash,
                    _fx.Block2.BlockHash,
                    _fx.Block3.BlockHash,
                },
                _set.Keys.ToHashSet());

            Assert.Equal(
                new HashSet<Block>()
                {
                    _fx.Block1,
                    _fx.Block2,
                    _fx.Block3,
                },
                _set.Values.ToHashSet());
        }

        [Fact]
        public void CanCountItem()
        {
            Assert.Empty(_set);

            _set[_fx.Block1.BlockHash] = _fx.Block1;
            Assert.Single(_set);

            _set[_fx.Block2.BlockHash] = _fx.Block2;
            _set[_fx.Block3.BlockHash] = _fx.Block3;
            Assert.Equal(3, _set.Count);
        }

        [Fact]
        public void CanDetectInvalidHash()
        {
            Assert.Throws<InvalidOperationException>(
                () => _set[_fx.Block1.BlockHash] = _fx.Block2);
        }

        [Fact]
        public void CanCheckContainsKey()
        {
            Assert.False(_set.ContainsKey(_fx.Block1.BlockHash));
            Assert.False(_set.ContainsKey(_fx.Block2.BlockHash));

            _set[_fx.Block1.BlockHash] = _fx.Block1;

            Assert.True(_set.ContainsKey(_fx.Block1.BlockHash));
            Assert.False(_set.ContainsKey(_fx.Block2.BlockHash));
        }

        [Fact]
        public void CanRemoveItem()
        {
            Assert.False(_set.Remove(_fx.Block1.BlockHash));
            _set[_fx.Block1.BlockHash] = _fx.Block1;
            Assert.True(_set.Remove(_fx.Block1.BlockHash));
            Assert.Throws<KeyNotFoundException>(() => { var val = _set[_fx.Block1.BlockHash]; });
        }

        public void Dispose()
        {
            _fx.Dispose();
        }
    }
}
