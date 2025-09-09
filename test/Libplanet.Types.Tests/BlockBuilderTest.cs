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
        var random = Rand.GetRandom(output);
        var signer = Rand.Signer(random);
        var height = Rand.Positive(random);
        var timestamp = DateTimeOffset.UtcNow;
        var previousBlockHash = Rand.BlockHash(random);
        var previousBlockCommit = Rand.BlockCommit(random);
        var previousStateRootHash = Rand.HashDigest<SHA256>(random);
        var transactions = Rand.ImmutableSortedSet(random, Rand.Transaction);
        var evidence = Rand.ImmutableSortedSet(random, Rand.Evidence);
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
