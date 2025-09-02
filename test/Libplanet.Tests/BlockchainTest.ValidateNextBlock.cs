using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Serialization;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests;

public partial class BlockchainTest
{
    [Fact]
    public void ValidateNextBlock()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var block = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = genesisBlock.Timestamp.AddDays(1),
                Proposer = proposer.Address,
                PreviousBlockHash = genesisBlock.BlockHash,
                PreviousStateRootHash = blockchain.StateRootHash,
            },
        }.Sign(proposer);
        var blockCommit = CreateBlockCommit(block);

        blockchain.Append(block, blockCommit);
        Assert.Equal(blockchain.Tip, block);
    }

    [Fact]
    public void ValidateNextBlockProtocolVersion()
    {
        var random = RandomUtility.GetRandom(_output);
        var blockVersion = 10;
        var proposer = RandomUtility.Signer(random);
        var repository = new Repository
        {
            BlockVersion = blockVersion,
        };
        var world = new World(repository.States).SetValidators(TestUtils.Validators).Commit();
        var genesisBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = blockVersion,
                Height = 0,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = proposer.Address,
                PreviousStateRootHash = world.Hash,
            },
        }.SignWithoutValidation(proposer);

        var blockchain = new Blockchain(genesisBlock, repository);

        var block1 = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = blockVersion,
                Height = 1,
                Timestamp = genesisBlock.Timestamp.AddDays(2),
                Proposer = proposer.Address,
                PreviousBlockHash = genesisBlock.BlockHash,
                PreviousBlockCommit = default,
                PreviousStateRootHash = blockchain.StateRootHash,
            },
        }.SignWithoutValidation(proposer);
        var blockCommit1 = CreateBlockCommit(block1);
        blockchain.Append(block1, blockCommit1);

        var block2 = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = blockVersion - 1,
                Height = 2,
                Timestamp = genesisBlock.Timestamp.AddDays(2),
                Proposer = proposer.Address,
                PreviousBlockHash = block1.BlockHash,
                PreviousBlockCommit = blockCommit1,
                PreviousStateRootHash = blockchain.StateRootHash,
            },
        }.SignWithoutValidation(proposer);
        var blockCommit2 = CreateBlockCommit(block2);
        Assert.Throws<ArgumentException>(() => blockchain.Append(block2, blockCommit2));

        var block3 = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = blockVersion + 1,
                Height = 2,
                Timestamp = genesisBlock.Timestamp.AddDays(2),
                Proposer = proposer.Address,
                PreviousBlockHash = block1.BlockHash,
                PreviousBlockCommit = blockCommit1,
                PreviousStateRootHash = blockchain.StateRootHash,
            },
        }.SignWithoutValidation(proposer);
        var blockCommit3 = CreateBlockCommit(block3);

        Assert.Throws<ArgumentException>(() => blockchain.Append(block3, blockCommit3));
    }

    [Fact]
    public void ValidateNextBlockInvalidIndex()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        blockchain.ProposeAndAppend(proposer);

        var block1 = blockchain.Tip;
        var blockA = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = block1.Height,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = proposer.Address,
                PreviousBlockHash = block1.BlockHash,
            },
        }.Sign(proposer);
        var blockCommitA = CreateBlockCommit(blockA);
        Assert.Throws<ArgumentException>(() => blockchain.Append(blockA, blockCommitA));

        var blockB = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = block1.Height + 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = proposer.Address,
                PreviousBlockHash = block1.BlockHash,
                PreviousBlockCommit = CreateBlockCommit(block1.BlockHash, block1.Height + 1, 0),
            },
        }.Sign(proposer);
        var blockCommitB = CreateBlockCommit(blockB);

        Assert.Throws<ArgumentException>(() => blockchain.Append(blockB, blockCommitB));
    }

    [Fact]
    public void ValidateNextBlockInvalidPreviousHash()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);

        var block2 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = proposer.Address,
                PreviousBlockHash = block1.PreviousBlockHash,
                PreviousBlockCommit = CreateBlockCommit(block1.PreviousBlockHash, 1, 0),
            },
        }.Sign(proposer);
        var blockCommit2 = CreateBlockCommit(block2);
        Assert.Throws<ArgumentException>(() => blockchain.Append(block2, blockCommit2));
    }

    [Fact]
    public void ValidateNextBlockInvalidTimestamp()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var (block1, blockCommit1) = blockchain.ProposeAndAppend(proposer);

        var block2 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = block1.Timestamp.AddSeconds(-1),
                Proposer = proposer.Address,
                PreviousBlockHash = block1.BlockHash,
                PreviousBlockCommit = blockCommit1,
            },
        }.Sign(proposer);
        var blockCommit2 = CreateBlockCommit(block2);
        Assert.Throws<ArgumentException>(() => blockchain.Append(block2, blockCommit2));
    }

    [Fact]
    public void ValidateNextBlockInvalidStateRootHash()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var optionsA = new BlockchainOptions
        {
        };
        var blockchainA = new Blockchain(genesisBlock, optionsA);

        var optionsB = new BlockchainOptions
        {
            SystemAction = new SystemAction
            {
                LeaveBlockActions = [new SetStatesAtBlock(default, "foo", default, 0)],
            },
        };
        var blockchainB = new Blockchain(genesisBlock, optionsB);

        var blockA = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddSeconds(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchainA.StateRootHash,
        }.Create(proposer);
        var blockCommitA = CreateBlockCommit(blockA);

        blockchainA.Append(blockA, blockCommitA);

        var blockB = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddSeconds(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = RandomUtility.HashDigest<SHA256>(random),
        }.Create(proposer);
        var blockCommitB = CreateBlockCommit(blockB);

        Assert.Throws<ArgumentException>(() => blockchainB.Append(blockB, blockCommitB));
    }

    [Fact]
    public void ValidateNextBlockLastCommitNullAtIndexOne()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var block1 = new BlockBuilder
        {
            Height = 1,
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit1 = CreateBlockCommit(block1);
        blockchain.Append(block1, blockCommit1);
        Assert.Equal(blockchain.Tip, block1);
    }

    [Fact]
    public void ValidateNextBlockLastCommitUpperIndexOne()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);

        var block2 = new BlockBuilder
        {
            Height = 2,
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = CreateBlockCommit(block1),
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);
        blockchain.Append(block2, blockCommit2);
        Assert.Equal(blockchain.Tip, block2);
    }

    [Fact]
    public void ValidateNextBlockLastCommitFailsUnexpectedValidator()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);

        var validators = TestValidators.Add(new(RandomUtility.Signer(random)));
        var votes = Enumerable.Range(0, validators.Count).Select(index => new VoteBuilder
        {
            Validator = validators[index],
            Block = block1,
            Type = VoteType.PreCommit,
        }.Create(validators[index])).ToImmutableArray();
        var invalidBlockCommit = new BlockCommit
        {
            Height = 1,
            BlockHash = block1.BlockHash,
            Votes = votes,
        };

        var block2 = new BlockBuilder
        {
            Height = 2,
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = invalidBlockCommit,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);
        Assert.Throws<ArgumentException>("block", () => blockchain.Append(block2, blockCommit2));
    }

    [Fact]
    public void ValidateNextBlockLastCommitFailsDropExpectedValidator()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);

        var validators = TestValidators.Remove(TestValidators[0]);
        var votes = validators.Select((signer, i) => new VoteBuilder
        {
            Validator = validators[i],
            Block = block1,
            Type = VoteType.PreCommit,
        }.Create(validators[i])).ToImmutableArray();
        var invalidBlockCommit = new BlockCommit
        {
            Height = 1,
            BlockHash = block1.BlockHash,
            Votes = votes,
        };
        var block2 = new BlockBuilder
        {
            Height = 2,
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = invalidBlockCommit,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);
        Assert.Throws<ArgumentException>("block", () => blockchain.Append(block2, blockCommit2));
    }

    [Fact]
    public void ValidateBlockCommitGenesis()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchainA = new Blockchain();
        // Works fine.
        blockchainA.Append(genesisBlock, default);

        // Should be null for genesis.
        var blockCommit = new BlockCommit
        {
            BlockHash = genesisBlock.BlockHash,
            Votes =
            [
                .. TestValidators.Select(validator => new Vote
                {
                    Metadata = new VoteMetadata
                    {
                        Height = 1,
                        BlockHash = RandomUtility.BlockHash(random),
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = validator.Address,
                        ValidatorPower = BigInteger.One,
                        Type = VoteType.PreCommit,
                    },
                    Signature = RandomUtility.ImmutableArray(random, RandomUtility.Byte),
                }),
            ],
        };
        var blockchainB = new Blockchain();
        Assert.Throws<ArgumentException>("blockCommit", () => blockchainB.Append(genesisBlock, blockCommit));
    }

    [Fact]
    public void ValidateBlockCommitFailsDifferentBlockHash()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var block1 = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddDays(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit1 = CreateBlockCommit(RandomUtility.BlockHash(random), 1, 0);

        Assert.Throws<ArgumentException>("blockCommit", () => blockchain.Append(block1, blockCommit1));
    }

    [Fact]
    public void ValidateBlockCommitFailsDifferentHeight()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var block1 = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddDays(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit1 = CreateBlockCommit(block1.BlockHash, 2, 0);

        Assert.Throws<ArgumentException>("blockCommit", () => blockchain.Append(block1, blockCommit1));
    }

    [Fact]
    public void ValidateBlockCommitFailsDifferentValidatorSet()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);

        var block1 = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddDays(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit1 = new BlockCommit
        {
            Height = 1,
            BlockHash = block1.BlockHash,
            Votes =
            [
                .. TestUtils.Validators.Select(CreatVote),
            ],
        };

        Assert.Throws<ArgumentException>(() => blockchain.Append(block1, blockCommit1));

        Vote CreatVote(Validator validator)
        {
            var metadata = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block1.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = validator.Address,
                ValidatorPower = BigInteger.One,
                Type = VoteType.PreCommit,
            };
            var options = new ModelOptions();
            var message = ModelSerializer.SerializeToBytes(metadata, options);
            var signer = RandomUtility.Signer(random);
            var signature = signer.Sign(message);
            return new Vote { Metadata = metadata, Signature = [.. signature] };
        }
    }

    [Fact]
    public void ValidateBlockCommitFailsNullBlockCommit()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var block1 = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddDays(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);

        Assert.Throws<ArgumentException>(() => blockchain.Append(block1, default));
    }

    [Fact]
    public void ValidateBlockCommitFailsInsufficientPower()
    {
        var random = RandomUtility.GetRandom(_output);
        ImmutableSortedSet<TestValidator> validators =
        [
            new TestValidator(RandomUtility.Signer(random), 10),
            new TestValidator(RandomUtility.Signer(random), 1),
            new TestValidator(RandomUtility.Signer(random), 1),
            new TestValidator(RandomUtility.Signer(random), 1),
        ];
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = [.. validators.Select(item => (Validator)item)],
        }.Create(proposer);

        static Vote CreateVote(TestValidator validator, BlockHash blockHash, VoteType voteType)
        {
            var metadata = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = blockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = validator.Address,
                ValidatorPower = validator.Power,
                Type = voteType,
            };
            return voteType == VoteType.Null ? metadata.WithoutSignature() : metadata.Sign(validator);
        }

        var blockchainA = new Blockchain(genesisBlock);
        var blockA = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddDays(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchainA.StateRootHash,
        }.Create(proposer);
        var blockCommitA = new BlockCommit
        {
            Height = 1,
            BlockHash = blockA.BlockHash,
            Votes =
            [
                CreateVote(validators[0], blockA.BlockHash, VoteType.PreCommit),
                CreateVote(validators[1], blockA.BlockHash, VoteType.PreCommit),
                CreateVote(validators[2], blockA.BlockHash, VoteType.PreCommit),
                CreateVote(validators[3], blockA.BlockHash, VoteType.PreCommit),
            ]
        };
        blockchainA.Append(blockA, blockCommitA);

        // Can propose if power is big enough even count condition is not met.
        var blockchainB = new Blockchain(genesisBlock);
        var blockB = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddDays(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchainB.StateRootHash,
        }.Create(proposer);
        var blockCommitB = new BlockCommit
        {
            Height = 1,
            BlockHash = blockA.BlockHash,
            Votes =
            [
                CreateVote(validators[0], blockB.BlockHash, VoteType.PreCommit),
                CreateVote(validators[1], blockB.BlockHash, VoteType.Null),
                CreateVote(validators[2], blockB.BlockHash, VoteType.Null),
                CreateVote(validators[3], blockB.BlockHash, VoteType.Null),
            ],
        };
        blockchainB.Append(blockB, blockCommitB);

        // Can not propose if power isn't big enough even count condition is met.
        var blockchainC = new Blockchain(genesisBlock);
        var blockC = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddDays(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchainC.StateRootHash,
        }.Create(proposer);
        var blockCommitC = new BlockCommit
        {
            Height = 1,
            BlockHash = blockA.BlockHash,
            Votes =
            [
                CreateVote(validators[0], blockC.BlockHash, VoteType.Null),
                CreateVote(validators[1], blockC.BlockHash, VoteType.PreCommit),
                CreateVote(validators[2], blockC.BlockHash, VoteType.PreCommit),
                CreateVote(validators[3], blockC.BlockHash, VoteType.PreCommit),
            ],
        };
        Assert.Throws<ArgumentException>("blockCommit", () => blockchainC.Append(blockC, blockCommitC));
    }

    [Fact]
    public void ValidateNextBlockOnChainRestart()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        var block = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddSeconds(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit = CreateBlockCommit(block);
        blockchain.Append(block, blockCommit);
        Assert.Equal(blockchain.Tip, block);
    }

    [Fact]
    public void ValidateNextBlockAEVChangedOnChainRestart()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            SystemAction = new SystemAction
            {
                LeaveBlockActions = [new SetStatesAtBlock(default, "foo", default, 0)],
            },
        };

        var blockchain = new Blockchain(genesisBlock, options);
        var (block1, blockCommit1) = blockchain.ProposeAndAppend(proposer);

        var block2 = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            Timestamp = block1.Timestamp.AddSeconds(1),
            PreviousBlockHash = block1.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit2 = CreateBlockCommit(block2);

        Assert.NotEqual(block1, block2);
        Assert.Throws<ArgumentException>("block", () => blockchain.Append(block1, blockCommit1));
        Assert.Throws<ArgumentException>("blockCommit", () => blockchain.Append(block2, blockCommit1));

        blockchain.Append(block2, blockCommit2);
    }
}
