using Libplanet.Tests.Store;
using Libplanet.Types;

namespace Libplanet.Net.Tests
{
    public class BlockCandidateTableTest
    {
        private readonly RepositoryFixture _fx;

        public BlockCandidateTableTest()
        {
            _fx = new MemoryRepositoryFixture();
        }

        [Fact]
        public void Add()
        {
            var table = new BlockCandidateTable();
            var header = _fx.GenesisBlock;

            // Ignore existing key
            var firstBranch = ImmutableSortedDictionary<Block, BlockCommit>.Empty
                    .Add(_fx.Block2, TestUtils.CreateBlockCommit(_fx.Block2))
                    .Add(_fx.Block3, TestUtils.CreateBlockCommit(_fx.Block3))
                    .Add(_fx.Block4, TestUtils.CreateBlockCommit(_fx.Block4));
            var secondBranch = ImmutableSortedDictionary<Block, BlockCommit>.Empty
                    .Add(_fx.Block3, TestUtils.CreateBlockCommit(_fx.Block3))
                    .Add(_fx.Block4, TestUtils.CreateBlockCommit(_fx.Block4));
            table.Add(header, firstBranch);
            Assert.Equal(1, table.Count);
            table.Add(header, secondBranch);
            Assert.Equal(1, table.Count);
            var branch = table.GetCurrentRoundCandidate(header)
                ?? throw new NullReferenceException();
            Assert.Equal(branch, firstBranch);
        }
    }
}
