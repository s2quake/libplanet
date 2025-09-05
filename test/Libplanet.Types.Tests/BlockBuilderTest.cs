using System.Security.Cryptography;
using Libplanet.TestUtilities;

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
        Assert.Empty(builder.Evidence);
    }

    [Fact]
    public void Create()
    {
        var random = RandomUtility.GetRandom(output);
        var signer = RandomUtility.Signer(random);
        var height = RandomUtility.Positive(random);
        var timestamp = DateTimeOffset.UtcNow;
        var previousBlockHash = RandomUtility.BlockHash(random);
        var previousBlockCommit = RandomUtility.BlockCommit(random);
        var previousStateRootHash = RandomUtility.HashDigest<SHA256>(random);
        var transactions = RandomUtility.ImmutableSortedSet(random, RandomUtility.Transaction);
        var evidence = RandomUtility.ImmutableSortedSet(random, RandomUtility.Evidence);
        var block = new BlockBuilder
        {
            Height = height,
            Timestamp = timestamp,
            PreviousBlockHash = previousBlockHash,
            PreviousBlockCommit = previousBlockCommit,
            PreviousStateRootHash = previousStateRootHash,
            Transactions = transactions,
            Evidence = evidence,
        }.Create(signer);

        Assert.Equal(BlockHeader.CurrentVersion, block.Version);
        Assert.Equal(height, block.Height);
        Assert.Equal(timestamp, block.Timestamp);
        Assert.Equal(previousBlockHash, block.PreviousBlockHash);
        Assert.Equal(previousBlockCommit, block.PreviousBlockCommit);
        Assert.Equal(previousStateRootHash, block.PreviousStateRootHash);
        Assert.Equal(transactions, block.Transactions);
        Assert.Equal(evidence, block.Evidences);
        Assert.Equal(signer.Address, block.Proposer);
    }
}
