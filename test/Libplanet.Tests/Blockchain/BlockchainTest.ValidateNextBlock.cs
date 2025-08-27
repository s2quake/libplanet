using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Extensions;
using Libplanet.Data;
using Libplanet.Types;
using Libplanet.TestUtilities;
using static Libplanet.Tests.TestUtils;
using System.Security.Cryptography;
using Xunit.Internal;

namespace Libplanet.Tests.Blockchain;

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
        var blockchain = new Libplanet.Blockchain(genesisBlock);
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
        var blockCommit = TestUtils.CreateBlockCommit(block);

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
        }.Sign(proposer);

        var blockchain = new Libplanet.Blockchain(genesisBlock, repository);

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
        }.Sign(proposer);
        var blockCommit1 = TestUtils.CreateBlockCommit(block1);
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
        }.Sign(proposer);
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
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
        }.Sign(proposer);
        var blockCommit3 = TestUtils.CreateBlockCommit(block3);

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
        var blockchain = new Libplanet.Blockchain(genesisBlock);

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
        var blockCommitA = TestUtils.CreateBlockCommit(blockA);
        Assert.Throws<ArgumentException>(() => blockchain.Append(blockA, blockCommitA));

        var blockB = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = block1.Height + 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = proposer.Address,
                PreviousBlockHash = block1.BlockHash,
                PreviousBlockCommit = TestUtils.CreateBlockCommit(block1.BlockHash, block1.Height + 1, 0),
            },
        }.Sign(proposer);
        var blockCommitB = TestUtils.CreateBlockCommit(blockB);

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
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);

        var block2 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = proposer.Address,
                PreviousBlockHash = block1.PreviousBlockHash,
                PreviousBlockCommit = TestUtils.CreateBlockCommit(block1.PreviousBlockHash, 1, 0),
            },
        }.Sign(proposer);
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
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
        var blockchain = new Libplanet.Blockchain(genesisBlock);
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
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
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
        var blockchainA = new Libplanet.Blockchain(genesisBlock, optionsA);

        var optionsB = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new SetStatesAtBlock(default, "foo", default, 0)],
            },
        };
        var blockchainB = new Libplanet.Blockchain(genesisBlock, optionsB);

        var blockA = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddSeconds(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchainA.StateRootHash,
        }.Create(proposer);
        var blockCommitA = TestUtils.CreateBlockCommit(blockA);

        blockchainA.Append(blockA, blockCommitA);

        var blockB = new BlockBuilder
        {
            Height = 1,
            Timestamp = genesisBlock.Timestamp.AddSeconds(1),
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = RandomUtility.HashDigest<SHA256>(random),
        }.Create(proposer);
        var blockCommitB = TestUtils.CreateBlockCommit(blockB);

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
        var blockchain = new Libplanet.Blockchain(genesisBlock);

        var block1 = new BlockBuilder
        {
            Height = 1,
            PreviousBlockHash = genesisBlock.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit1 = TestUtils.CreateBlockCommit(block1);
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
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);

        var block2 = new BlockBuilder
        {
            Height = 2,
            PreviousBlockHash = block1.BlockHash,
            PreviousBlockCommit = TestUtils.CreateBlockCommit(block1),
            PreviousStateRootHash = blockchain.StateRootHash,
        }.Create(proposer);
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
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
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);

        var invalidValidator = RandomUtility.Try(random, RandomUtility.Signer, v => v.Address > Signers[3].Address);
        var signers = TestUtils.Signers.Add(invalidValidator);
        var validators = TestUtils.Validators.Add(new Validator { Address = invalidValidator.Address });
        var votes = Enumerable.Range(0, validators.Count).Select(index => new VoteBuilder
        {
            Validator = validators[index],
            Block = block1,
            Type = VoteType.PreCommit,
        }.Create(signers[index])).ToImmutableArray();
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
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
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
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var (block1, _) = blockchain.ProposeAndAppend(proposer);

        var signers = TestUtils.Signers.Except([Signers[0]]).ToArray();
        var validators = TestUtils.Validators.Except([TestUtils.Validators[0]]).ToImmutableSortedSet();
        var votes = signers.Select((signer, i) => new VoteBuilder
        {
            Validator = validators[i],
            Block = block1,
            Type = VoteType.PreCommit,
        }.Create(signers[i])).ToImmutableArray();
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
        var blockCommit2 = TestUtils.CreateBlockCommit(block2);
        Assert.Throws<ArgumentException>("block", () => blockchain.Append(block2, blockCommit2));
    }

    [Fact]
    public void ValidateBlockCommitGenesis()
    {
        // Works fine.
        // _blockChain.ValidateBlockCommit(_fx.GenesisBlock, default);

        // Should be null for genesis.
        var blockCommit = new BlockCommit
        {
            BlockHash = _fx.GenesisBlock.BlockHash,
            Votes = [.. TestUtils.Signers.Select(x => new VoteMetadata
            {
                Height = 0,
                Round = 0,
                BlockHash = _fx.GenesisBlock.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = x.Address,
                ValidatorPower = TestUtils.Validators.GetValidator(x.Address).Power,
                Type = VoteType.PreCommit,
            }.Sign(x))],
        };
        Assert.Throws<InvalidOperationException>(() => blockCommit.Validate(_fx.GenesisBlock));
    }

    [Fact]
    public void ValidateBlockCommitFailsDifferentBlockHash()
    {
        Block validNextBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                Proposer = _fx.Proposer.Address,
                PreviousBlockHash = _fx.GenesisBlock.BlockHash,
            },
        }.Sign(_fx.Proposer);

        Assert.Throws<InvalidOperationException>(() =>
            _blockchain.Append(
                validNextBlock,
                TestUtils.CreateBlockCommit(
                    new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    1,
                    0)));
    }

    [Fact]
    public void ValidateBlockCommitFailsDifferentHeight()
    {
        Block validNextBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                Proposer = _fx.Proposer.Address,
                PreviousBlockHash = _fx.GenesisBlock.BlockHash,
            },
        }.Sign(_fx.Proposer);

        Assert.Throws<InvalidOperationException>(() =>
            _blockchain.Append(
                validNextBlock,
                TestUtils.CreateBlockCommit(
                    validNextBlock.BlockHash,
                    2,
                    0)));
    }

    [Fact]
    public void ValidateBlockCommitFailsDifferentValidatorSet()
    {
        var random = RandomUtility.GetRandom(_output);
        var validNextBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                Proposer = _fx.Proposer.Address,
                PreviousBlockHash = _fx.GenesisBlock.BlockHash,
                PreviousStateRootHash = _blockchain.StateRootHash,
            },
        }.Sign(_fx.Proposer);

        Assert.Throws<InvalidOperationException>(() =>
            _blockchain.Append(
                validNextBlock,
                new BlockCommit
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = validNextBlock.BlockHash,
                    Votes = [.. Enumerable.Range(0, TestUtils.Validators.Count)
                        .Select(x => RandomUtility.Signer(random))
                        .Select(x => new VoteMetadata
                        {
                            Height = 1,
                            Round = 0,
                            BlockHash = validNextBlock.BlockHash,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = x.Address,
                            ValidatorPower = BigInteger.One,
                            Type = VoteType.PreCommit,
                        }.Sign(x))],
                }));
    }

    [Fact]
    public void ValidateBlockCommitFailsNullBlockCommit()
    {
        Block validNextBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                Proposer = _fx.Proposer.Address,
                PreviousBlockHash = _fx.GenesisBlock.BlockHash,
            },
        }.Sign(_fx.Proposer);

        Assert.Throws<InvalidOperationException>(() =>
            _blockchain.Append(validNextBlock, default));
    }

    [Fact]
    public void ValidateBlockCommitFailsInsufficientPower()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer1 = RandomUtility.Signer(random);
        var signer2 = RandomUtility.Signer(random);
        var signer3 = RandomUtility.Signer(random);
        var signer4 = RandomUtility.Signer(random);
        var validator1 = new Validator { Address = signer1.Address, Power = 10 };
        var validator2 = new Validator { Address = signer2.Address, Power = 1 };
        var validator3 = new Validator { Address = signer3.Address, Power = 1 };
        var validator4 = new Validator { Address = signer4.Address, Power = 1 };
        ImmutableSortedSet<Validator> validatorSet
            = [validator1, validator2, validator3, validator4];
        Libplanet.Blockchain blockChain = TestUtils.MakeBlockchain(
            validators: validatorSet);
        Block validNextBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = blockChain.Genesis.Timestamp.AddDays(1),
                Proposer = _fx.Proposer.Address,
                PreviousBlockHash = blockChain.Genesis.BlockHash,
            },
        }.Sign(_fx.Proposer);

        Vote GenerateVote(ISigner signer, BigInteger power, VoteType flag)
        {
            var metadata = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = validNextBlock.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = signer.Address,
                ValidatorPower = power,
                Type = flag,
            };
            return flag == VoteType.Null
                ? metadata.WithoutSignature()
                : metadata.Sign(signer);
        }

        ImmutableArray<Vote> GenerateVotes(
            VoteType flag1,
            VoteType flag2,
            VoteType flag3,
            VoteType flag4)
        {
            return new[]
            {
                GenerateVote(signer1, validator1.Power, flag1),
                GenerateVote(signer2, validator2.Power, flag2),
                GenerateVote(signer3, validator3.Power, flag3),
                GenerateVote(signer4, validator4.Power, flag4),
            }.OrderBy(vote => vote.Validator).ToImmutableArray();
        }

        var fullBlockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = validNextBlock.BlockHash,
            Votes = GenerateVotes(
                VoteType.PreCommit,
                VoteType.PreCommit,
                VoteType.PreCommit,
                VoteType.PreCommit),
        };
        fullBlockCommit.Validate(validNextBlock);

        // Can propose if power is big enough even count condition is not met.
        var validBlockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = validNextBlock.BlockHash,
            Votes = GenerateVotes(
                VoteType.PreCommit,
                VoteType.Null,
                VoteType.Null,
                VoteType.Null),
        };
        validBlockCommit.Validate(validNextBlock);

        // Can not propose if power isn't big enough even count condition is met.
        var invalidBlockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = validNextBlock.BlockHash,
            Votes = GenerateVotes(
                VoteType.Null,
                VoteType.PreCommit,
                VoteType.PreCommit,
                VoteType.PreCommit),
        };
        Assert.Throws<InvalidOperationException>(() => invalidBlockCommit.Validate(validNextBlock));
    }

    [Fact]
    public void ValidateNextBlockOnChainRestart()
    {
        var repository = new Repository();
        var newChain = new Libplanet.Blockchain(_blockchain.Genesis, repository, _options);
        newChain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext));
        Assert.Equal(newChain.Tip, _validNext);
    }

    [Fact]
    public void ValidateNextBlockAEVChangedOnChainRestart()
    {
        var endBlockActions =
            new IAction[] { new SetStatesAtBlock(default, "foo", default, 0), }
                .ToImmutableArray();
        var policyWithBlockAction = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = endBlockActions,
            },
        };

        var repository = new Repository();
        var newChain = new Libplanet.Blockchain(_blockchain.Genesis, repository, policyWithBlockAction);

        Block newValidNext = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = BlockHeader.CurrentProtocolVersion,
                Height = newChain.Tip.Height + 1,
                Timestamp = newChain.Tip.Timestamp.AddSeconds(1),
                Proposer = TestUtils.GenesisProposer.Address,
                PreviousBlockHash = newChain.Tip.BlockHash,
            },
        }.Sign(TestUtils.GenesisProposer);

        Assert.NotEqual(_validNext, newValidNext);

        Assert.Throws<InvalidOperationException>(() =>
            newChain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext)));

        newChain.Append(newValidNext, TestUtils.CreateBlockCommit(newValidNext));
    }
}
