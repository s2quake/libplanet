using Libplanet.Types;

namespace Libplanet.TestUtilities;

public static class TestUtility
{
    public static BlockCommit CreateBlockCommit(Block block, ImmutableSortedSet<TestValidator> validators) => new()
    {
        Height = block.Height,
        Round = 0,
        BlockHash = block.BlockHash,
        Votes =
        [
            .. validators.Select(v => new VoteBuilder
            {
                Validator = v,
                Block = block,
                Round = 0,
                Timestamp = DateTimeOffset.UtcNow,
                Type = VoteType.PreCommit,
            }.Create(v)),
        ],
    };
}
