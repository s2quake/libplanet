using BenchmarkDotNet.Attributes;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Benchmarks;

public class Commit
{
    private const int MaxValidatorSize = 100;

    private Vote[] _votes = [];
    private ImmutableSortedSet<TestValidator> _validators = [];
    private BlockHash _blockHash;
    private BlockCommit _blockCommit;
    private byte[] _encodedBlockCommit = [];

    [Params(4, 10, 25, 50, MaxValidatorSize)]
    public int ValidatorSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _blockHash = RandomUtility.BlockHash();
        SetupKeys();
        SetupVotes();
    }

    [IterationSetup(Target = nameof(DecodeBlockCommit))]
    public void PrepareDecode()
    {
        _blockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = _blockHash,
            Votes = [.. _votes.Take(ValidatorSize)],
        };
        _encodedBlockCommit = ModelSerializer.SerializeToBytes(_blockCommit);
    }

    [Benchmark]
    public void DecodeBlockCommit()
    {
        _blockCommit = ModelSerializer.DeserializeFromBytes<BlockCommit>(_encodedBlockCommit);
    }

    private void SetupKeys()
    {
        _validators = RandomUtility.ImmutableSortedSet(RandomUtility.TestValidator, MaxValidatorSize);
    }

    private void SetupVotes()
    {
        _votes =
        [
            .. Enumerable.Range(0, MaxValidatorSize)
                .Select(x => _validators[x].CreateVote(1, 0, _blockHash, VoteType.PreCommit))
        ];
    }
}
