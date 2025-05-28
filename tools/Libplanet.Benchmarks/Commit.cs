using BenchmarkDotNet.Attributes;
using Libplanet.Serialization;
using Libplanet.Tests;
using Libplanet.Types;

namespace Libplanet.Benchmarks
{
    public class Commit
    {
        private const int MaxValidatorSize = 100;

        private Vote[] _votes;
        private PrivateKey[] _privateKeys;
        private BlockHash _blockHash;
        private BlockCommit _blockCommit;
        private byte[] _encodedBlockCommit;

        [Params(4, 10, 25, 50, MaxValidatorSize)]
        // ReSharper disable once MemberCanBePrivate.Global
        // System.InvalidOperationException: Member "ValidatorSize" must be public if it has the
        // [ParamsAttribute] attribute applied to it
        public int ValidatorSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _blockHash = new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size));
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
            _privateKeys = new PrivateKey[MaxValidatorSize];
            for (int i = 0; i < MaxValidatorSize; i++)
            {
                _privateKeys[i] = new PrivateKey();
            }
        }

        private void SetupVotes()
        {
            _votes = [.. Enumerable.Range(0, MaxValidatorSize)
                .Select(x =>
                    new VoteMetadata
                    {
                        Height = 1,
                        Round = 0,
                        BlockHash = _blockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = _privateKeys[x].Address,
                        ValidatorPower = BigInteger.One,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(_privateKeys[x]))];
        }
    }
}
