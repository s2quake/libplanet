using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Data.LiteDB.Tests;

public sealed class LiteBlockHashIndexTest(ITestOutputHelper output)
    : LiteIndexTestBase<int, BlockHash, BlockHashIndex>(output)
{
    protected override BlockHashIndex CreateIndex(LiteDatabase database, bool useCache)
        => new(database, useCache ? 100 : 0);

    protected override int CreateKey(Random random) => RandomUtility.Int32(random);

    protected override BlockHash CreateValue(Random random) => RandomUtility.BlockHash(random);

    [Fact]
    public void AddBlock()
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(AddBlock), useCache: true);
        var signer = RandomUtility.Signer(random);
        var block0 = new BlockBuilder
        {
            Height = 0,
        }.Create(signer);
        var block2 = new BlockBuilder
        {
            Height = 2,
        }.Create(signer);
        index.Add(block0);
        index.Height = block0.Height;

        Assert.Equal(block0.BlockHash, index[block0.Height]);
        Assert.Single(index[block0.Height..]);

        index.Add(block2);
        index.Height = block2.Height;

        Assert.Single(index[block2.Height..]);
        Assert.Equal(2, index[block0.Height..].Count());
    }
}
