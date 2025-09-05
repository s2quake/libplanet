namespace Libplanet.Types.Tests;

public sealed class BlockBuilderTest(ITestOutputHelper output)
{
    [Fact]
    public void BaseTest()
    {
        var builder = new BlockBuilder();

        Assert.Equal(0, builder.Height);
        Assert.Equal(default, builder.Timestamp);
        Assert.Equal(default, builder.PreviousBlockHash);
        Assert.Equal(default, builder.PreviousBlockCommit);
        Assert.Equal(default, builder.PreviousStateRootHash);
        Assert.Empty(builder.Transactions);
        Assert.Empty(builder.Evidences);
    }
}
