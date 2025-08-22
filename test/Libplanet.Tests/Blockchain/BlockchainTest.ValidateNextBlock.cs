using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Extensions;
using Libplanet.Data;
using Libplanet.Types;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;

namespace Libplanet.Tests.Blockchain;

public partial class BlockchainTest
{
    [Fact]
    public void ValidateNextBlock()
    {
        var block = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                Proposer = _fx.Proposer.Address,
                PreviousHash = _fx.GenesisBlock.BlockHash,
                PreviousStateRootHash = _blockchain.StateRootHash,
            },
        }.Sign(_fx.Proposer);
        var blockCommit = TestUtils.CreateBlockCommit(block);

        _blockchain.Append(block, blockCommit);
        Assert.Equal(_blockchain.Tip, block);
    }

    [Fact]
    public void ValidateNextBlockProtocolVersion()
    {
        var protocolVersion = _blockchain.Tip.Version;
        var block1 = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = protocolVersion,
                Height = 1,
                Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                Proposer = _fx.Proposer.Address,
                PreviousHash = _fx.GenesisBlock.BlockHash,
                PreviousStateRootHash = _blockchain.StateRootHash,
            },
        }.Sign(_fx.Proposer);
        var blockCommit1 = TestUtils.CreateBlockCommit(block1);
        _blockchain.Append(block1, blockCommit1);

        Assert.Throws<ApplicationException>(() =>
            new RawBlock
            {
                Header = new BlockHeader
                {
                    Height = 2,
                    Timestamp = _fx.GenesisBlock.Timestamp.AddDays(2),
                    Proposer = _fx.Proposer.Address,
                    PreviousHash = block1.BlockHash,
                },
            }.Sign(_fx.Proposer));

        Assert.Throws<InvalidOperationException>(() =>
        {
            Block block3 = new RawBlock
            {
                Header = new BlockHeader
                {
                    BlockVersion = BlockHeader.CurrentProtocolVersion + 1,
                    Height = 2,
                    Timestamp = _fx.GenesisBlock.Timestamp.AddDays(2),
                    Proposer = _fx.Proposer.Address,
                    PreviousHash = block1.BlockHash,
                },
            }.Sign(_fx.Proposer);
            _blockchain.Append(block3, TestUtils.CreateBlockCommit(block3));
        });
    }

    [Fact]
    public void ValidateNextBlockInvalidIndex()
    {
        _blockchain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext));

        Block prev = _blockchain.Tip;
        Block blockWithAlreadyUsedIndex = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = prev.Height,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = prev.BlockHash,
            },
        }.Sign(_fx.Proposer);
        Assert.Throws<InvalidOperationException>(
            () => _blockchain.Append(
                blockWithAlreadyUsedIndex,
                TestUtils.CreateBlockCommit(blockWithAlreadyUsedIndex)));

        Block blockWithIndexAfterNonexistentIndex = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = prev.Height + 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = prev.BlockHash,
                PreviousCommit = TestUtils.CreateBlockCommit(prev.BlockHash, prev.Height + 1, 0),
            },
        }.Sign(_fx.Proposer);
        Assert.Throws<InvalidOperationException>(
            () => _blockchain.Append(
                blockWithIndexAfterNonexistentIndex,
                TestUtils.CreateBlockCommit(blockWithIndexAfterNonexistentIndex)));
    }

    [Fact]
    public void ValidateNextBlockInvalidPreviousHash()
    {
        _blockchain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext));

        Block invalidPreviousHashBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                // Should be _validNext.Hash instead
                PreviousHash = _validNext.PreviousHash,
                // ReSharper disable once PossibleInvalidOperationException
                PreviousCommit = TestUtils.CreateBlockCommit(
                        _validNext.PreviousHash, 1, 0),
            },
        }.Sign(_fx.Proposer);
        Assert.Throws<InvalidOperationException>(() =>
                _blockchain.Append(
                    invalidPreviousHashBlock,
                    TestUtils.CreateBlockCommit(invalidPreviousHashBlock)));
    }

    [Fact]
    public void ValidateNextBlockInvalidTimestamp()
    {
        _blockchain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext));

        Block invalidPreviousTimestamp = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = _validNext.Timestamp.AddSeconds(-1),
                Proposer = _fx.Proposer.Address,
                PreviousHash = _validNext.BlockHash,
                PreviousCommit = TestUtils.CreateBlockCommit(_validNext),
            },
        }.Sign(_fx.Proposer);
        Assert.Throws<InvalidOperationException>(() =>
                _blockchain.Append(
                    invalidPreviousTimestamp,
                    TestUtils.CreateBlockCommit(invalidPreviousTimestamp)));
    }

    [Fact]
    public void ValidateNextBlockInvalidStateRootHash()
    {
        var options = new BlockchainOptions
        {
            // BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
        };
        var genesisBlock = TestUtils.ProposeGenesis(TestUtils.GenesisProposer).Sign(TestUtils.GenesisProposer);
        var repository = new Repository();

        var chain1 = new Libplanet.Blockchain(genesisBlock, repository, options);
        var endBlockActions = new IAction[]
        {
            new SetStatesAtBlock(default, "foo", default, 0),
        }.ToImmutableArray();
        var options2 = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new SetStatesAtBlock(default, "foo", default, 0)],
            },
            // BlockInterval = options.BlockInterval,
        };
        var repository2 = new Repository();
        var chain2 = new Libplanet.Blockchain(genesisBlock, repository2, options2);

        Block block1 = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = BlockHeader.CurrentProtocolVersion,
                Height = 1,
                Timestamp = genesisBlock.Timestamp.AddSeconds(1),
                Proposer = TestUtils.GenesisProposer.Address,
                PreviousHash = genesisBlock.BlockHash,
            },
        }.Sign(TestUtils.GenesisProposer);

        Assert.Throws<InvalidOperationException>(() =>
            chain2.Append(block1, TestUtils.CreateBlockCommit(block1)));

        chain1.Append(block1, TestUtils.CreateBlockCommit(block1));
    }

    [Fact]
    public void ValidateNextBlockInvalidStateRootHashBeforePostpone()
    {
        var beforePostponeBPV = BlockHeader.CurrentProtocolVersion;
        var options1 = new BlockchainOptions
        {
            // BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
        };
        var repository = new Repository();
        var blockExecutor = new BlockExecutor(
            repository.States,
            options1.SystemActions);
        var preGenesis = TestUtils.ProposeGenesis(
            proposer: TestUtils.GenesisProposer,
            protocolVersion: beforePostponeBPV);
        var preExecution = blockExecutor.Execute(preGenesis);
        var genesisBlock = preGenesis.Sign(TestUtils.GenesisProposer);
        var chain1 = new Libplanet.Blockchain(genesisBlock, repository, options1);

        Block block1 = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = beforePostponeBPV,
                Height = 1,
                Timestamp = genesisBlock.Timestamp.AddSeconds(1),
                Proposer = TestUtils.GenesisProposer.Address,
                PreviousHash = genesisBlock.BlockHash,
            },
        }.Sign(TestUtils.GenesisProposer);

        var options2 = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                BeginBlockActions = [],
                EndBlockActions = [new SetStatesAtBlock(default, "foo", default, 1)],
            },
            // BlockInterval = options1.BlockInterval,
        };
        var repository2 = new Repository();
        var chain2 = new Libplanet.Blockchain(genesisBlock, repository2, options2);

        Assert.Throws<InvalidOperationException>(() =>
            chain2.Append(block1, TestUtils.CreateBlockCommit(block1)));

        chain1.Append(block1, TestUtils.CreateBlockCommit(block1));
    }

    [Fact]
    public void ValidateNextBlockInvalidStateRootHashOnPostpone()
    {
        var beforePostponeBPV = BlockHeader.CurrentProtocolVersion;
        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                BeginBlockActions = [new SetStatesAtBlock(default, "foo", default, 1)],
            },
            // BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
        };
        var repository = new Repository();
        var blockExecutor = new BlockExecutor(
            repository.States,
            options.SystemActions);
        var rawGenesis = TestUtils.ProposeGenesis(
            proposer: TestUtils.GenesisProposer,
            protocolVersion: beforePostponeBPV);
        var rawEvaluation = blockExecutor.Execute(rawGenesis);
        var genesisBlock = rawGenesis.Sign(TestUtils.GenesisProposer);
        var chain = new Libplanet.Blockchain(genesisBlock, repository, options);

        RawBlock preBlock1 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = genesisBlock.Timestamp.AddSeconds(1),
                Proposer = TestUtils.GenesisProposer.Address,
                PreviousHash = genesisBlock.BlockHash,
            },
        };
        Block block1 = preBlock1.Sign(TestUtils.GenesisProposer);
        Assert.Equal(genesisBlock.PreviousStateRootHash, block1.PreviousStateRootHash);

        Block block2 = preBlock1.Sign(
            TestUtils.GenesisProposer);

        Assert.Throws<InvalidOperationException>(() =>
            chain.Append(block2, TestUtils.CreateBlockCommit(block2)));

        chain.Append(block1, TestUtils.CreateBlockCommit(block1));
    }

    [Fact]
    public void ValidateNextBlockLastCommitNullAtIndexOne()
    {
        Block validNextBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = _fx.GenesisBlock.BlockHash,
            },
        }.Sign(_fx.Proposer);
        _blockchain.Append(validNextBlock, TestUtils.CreateBlockCommit(validNextBlock));
        Assert.Equal(_blockchain.Tip, validNextBlock);
    }

    [Fact]
    public void ValidateNextBlockLastCommitUpperIndexOne()
    {
        Block block1 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = _fx.GenesisBlock.BlockHash,
            },
        }.Sign(_fx.Proposer);
        _blockchain.Append(block1, TestUtils.CreateBlockCommit(block1));

        var blockCommit = TestUtils.CreateBlockCommit(block1);
        Block block2 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = block1.BlockHash,
                PreviousCommit = blockCommit,
            },
        }.Sign(_fx.Proposer);
        _blockchain.Append(block2, TestUtils.CreateBlockCommit(block2));
        Assert.Equal(_blockchain.Tip, block2);
    }

    [Fact]
    public void ValidateNextBlockLastCommitFailsUnexpectedValidator()
    {
        var random = RandomUtility.GetRandom(_output);
        Block block1 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = _fx.GenesisBlock.BlockHash,
            },
        }.Sign(_fx.Proposer);
        _blockchain.Append(block1, TestUtils.CreateBlockCommit(block1));

        var invalidValidator = RandomUtility.Signer(random);
        var validators = TestUtils.Signers.Append(invalidValidator).ToList();
        var validatorPowers = TestUtils.Validators.Select(v => v.Power)
            .Append(BigInteger.One)
            .ToList();
        var votes = Enumerable.Range(0, validators.Count).Select(index => new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block1.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = validators[index].Address,
            ValidatorPower = validatorPowers[index],
            Type = VoteType.PreCommit,
        }.Sign(validators[index])).ToImmutableArray();
        var blockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = block1.BlockHash,
            Votes = votes,
        };

        Block block2 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = block1.BlockHash,
                PreviousCommit = blockCommit,
            },
        }.Sign(_fx.Proposer);
        Assert.Throws<InvalidOperationException>(() =>
            _blockchain.Append(block2, TestUtils.CreateBlockCommit(block2)));
    }

    [Fact]
    public void ValidateNextBlockLastCommitFailsDropExpectedValidator()
    {
        Block block1 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = _fx.GenesisBlock.BlockHash,
            },
        }.Sign(_fx.Proposer);
        _blockchain.Append(block1, TestUtils.CreateBlockCommit(block1));

        var keysExceptPeer0 = TestUtils.Signers.Where(
            key => key != TestUtils.Signers[0]).ToList();
        var votes = keysExceptPeer0.Select(key => new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block1.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = key.Address,
            ValidatorPower = TestUtils.Validators.GetValidator(key.Address).Power,
            Type = VoteType.PreCommit,
        }.Sign(key)).ToImmutableArray();
        var blockCommit = new BlockCommit
        {
            Height = 1,
            Round = 0,
            BlockHash = block1.BlockHash,
            Votes = votes,
        };
        Block block2 = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 2,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = _fx.Proposer.Address,
                PreviousHash = block1.BlockHash,
                PreviousCommit = blockCommit,
            },
        }.Sign(_fx.Proposer);
        Assert.Throws<InvalidOperationException>(() =>
            _blockchain.Append(block2, TestUtils.CreateBlockCommit(block2)));
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
                PreviousHash = _fx.GenesisBlock.BlockHash,
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
                PreviousHash = _fx.GenesisBlock.BlockHash,
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
                PreviousHash = _fx.GenesisBlock.BlockHash,
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
                PreviousHash = _fx.GenesisBlock.BlockHash,
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
                PreviousHash = blockChain.Genesis.BlockHash,
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
                PreviousHash = newChain.Tip.BlockHash,
            },
        }.Sign(TestUtils.GenesisProposer);

        Assert.NotEqual(_validNext, newValidNext);

        Assert.Throws<InvalidOperationException>(() =>
            newChain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext)));

        newChain.Append(newValidNext, TestUtils.CreateBlockCommit(newValidNext));
    }
}
