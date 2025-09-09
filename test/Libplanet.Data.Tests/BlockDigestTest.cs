using Libplanet.TestUtilities;

namespace Libplanet.Data.Tests;

public sealed class BlockDigestTest(ITestOutputHelper output)
{
    [Fact]
    public void Test()
    {
        var random = Rand.GetRandom(output);
        var blockDigest = Rand.BlockDigest(random);
        Assert.Equal(blockDigest.Height, blockDigest.Header.Height);
        Assert.Equal(blockDigest.Proposer, blockDigest.Header.Proposer);
        Assert.Equal(blockDigest.PreviousHash, blockDigest.Header.PreviousBlockHash);

        Assert.Equal(blockDigest, blockDigest with { });
    }
}
