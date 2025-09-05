using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.State.Tests;

public class ActionContextTest(ITestOutputHelper output)
{
    [Fact]
    public void RandomShouldBeDeterministic()
    {
        var random = RandomUtility.GetRandom(output);
        var address = RandomUtility.Address(random);
        var txId = RandomUtility.TxId(random);
        var signer = RandomUtility.Signer(random);
        var blockHash = RandomUtility.BlockHash(random);
        var lastCommit = new BlockCommit
        {
            BlockHash = blockHash,
            Votes =
            [
                new VoteMetadata
                {
                    BlockHash = blockHash,
                    Height = 1,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = signer.Address,
                    ValidatorPower = BigInteger.One,
                    Type = VoteType.PreCommit,
                }.Sign(signer),
            ],
        };
        (int Seed, int Expected)[] testCases =
        [
            (0, 1559595546),
            (1, 534011718),
        ];
        foreach (var (seed, expected) in testCases)
        {
            var context = new ActionContext
            {
                Signer = address,
                TxId = txId,
                Proposer = address,
                BlockHeight = 1,
                BlockProtocolVersion = BlockHeader.CurrentVersion,
                PreviousCommit = lastCommit,
                RandomSeed = seed,
            };
            Assert.Equal(expected, context.GetRandom().Next());
        }
    }

    [Fact]
    public void GuidShouldBeDeterministic()
    {
        var random = RandomUtility.GetRandom(output);
        var address = RandomUtility.Address(random);
        var txId = RandomUtility.TxId(random);
        var signer = RandomUtility.Signer(random);
        var blockHash = RandomUtility.BlockHash(random);
        var lastCommit = new BlockCommit
        {
            BlockHash = blockHash,
            Votes =
            [
                new VoteMetadata
                {
                    BlockHash = blockHash,
                    Height = 1,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = signer.Address,
                    ValidatorPower = BigInteger.One,
                    Type = VoteType.PreCommit,
                }.Sign(signer),
            ],
        };
        var actionContext1 = new ActionContext
        {
            Signer = address,
            TxId = txId,
            Proposer = address,
            BlockHeight = 1,
            BlockProtocolVersion = BlockHeader.CurrentVersion,
            PreviousCommit = lastCommit,
            RandomSeed = 0,
        };

        var actionContext2 = new ActionContext
        {
            Signer = address,
            TxId = txId,
            Proposer = address,
            BlockHeight = 1,
            BlockProtocolVersion = BlockHeader.CurrentVersion,
            PreviousCommit = lastCommit,
        };

        var actionContext3 = new ActionContext
        {
            Signer = address,
            TxId = txId,
            Proposer = address,
            BlockHeight = 1,
            BlockProtocolVersion = BlockHeader.CurrentVersion,
            PreviousCommit = lastCommit,
            RandomSeed = 1,
        };

        (Guid Expected, Guid Diff)[] testCases =
        [
            (new Guid("6f460c1a-755d-48e4-ad67-65d5f519dbc8"), new Guid("8286d046-9740-43e4-95cf-ff46699c73c4")),
            (new Guid("3b347c2b-f837-4085-ac5e-64005393b30d"), new Guid("3410cda1-5b13-434e-af84-a54adf7a0ea0")),
        ];

        var random1 = actionContext1.GetRandom();
        var random2 = actionContext2.GetRandom();
        var random3 = actionContext3.GetRandom();
        foreach (var (expected, diff) in testCases)
        {
            Assert.Equal(expected, random1.NextGuid());
            Assert.Equal(expected, random2.NextGuid());
            Assert.Equal(diff, random3.NextGuid());
        }
    }

    [Fact]
    public void GuidVersionAndVariant()
    {
        var random = RandomUtility.GetRandom(output);
        var address = RandomUtility.Address(random);
        var txId = RandomUtility.TxId(random);
        var signer = RandomUtility.Signer(random);
        var blockHash = RandomUtility.BlockHash(random);
        var lastCommit = new BlockCommit
        {
            BlockHash = blockHash,
            Votes =
            [
                new VoteMetadata
                {
                    BlockHash = blockHash,
                    Height = 1,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = signer.Address,
                    ValidatorPower = BigInteger.One,
                    Type = VoteType.PreCommit,
                }.Sign(signer),
            ],
        };
        for (var i = 0; i < 100; i++)
        {
            var context = new ActionContext
            {
                Signer = address,
                TxId = txId,
                Proposer = address,
                BlockHeight = 1,
                BlockProtocolVersion = BlockHeader.CurrentVersion,
                PreviousCommit = lastCommit,
                RandomSeed = i,
            };
            var guid = context.GetRandom().NextGuid().ToString();

            Assert.Equal('4', guid[14]);
            Assert.True(guid[19] >= '8' && guid[19] <= 'b');
        }
    }
}
