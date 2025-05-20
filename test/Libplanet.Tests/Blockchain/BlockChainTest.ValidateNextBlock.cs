using Libplanet.Action;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Tests.Blockchain
{
    public partial class BlockChainTest
    {
        [Fact]
        public void ValidateNextBlock()
        {
            Block validNextBlock = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);
            _blockChain.Append(validNextBlock, TestUtils.CreateBlockCommit(validNextBlock));
            Assert.Equal(_blockChain.Tip, validNextBlock);
        }

        [Fact]
        public void ValidateNextBlockProtocolVersion()
        {
            var protocolVersion = _blockChain.Tip.Version;
            Block block1 = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Version = protocolVersion,
                        Height = 1,
                        Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);
            _blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));

            Assert.Throws<ApplicationException>(() => _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 2,
                        Timestamp = _fx.GenesisBlock.Timestamp.AddDays(2),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = block1.BlockHash,
                    },
                },
                _fx.Proposer));

            Assert.Throws<InvalidOperationException>(() =>
            {
                Block block3 = _blockChain.EvaluateAndSign(
                    new RawBlock
                    {
                        Header = new BlockHeader
                        {
                            Version = BlockHeader.CurrentProtocolVersion + 1,
                            Height = 2,
                            Timestamp = _fx.GenesisBlock.Timestamp.AddDays(2),
                            Proposer = _fx.Proposer.Address,
                            PreviousHash = block1.BlockHash,
                        },
                    },
                    _fx.Proposer);
                _blockChain.Append(block3, TestUtils.CreateBlockCommit(block3));
            });
        }

        [Fact]
        public void ValidateNextBlockInvalidIndex()
        {
            _blockChain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext));

            Block prev = _blockChain.Tip;
            Block blockWithAlreadyUsedIndex = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = prev.Height,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = prev.BlockHash,
                    },
                },
                _fx.Proposer);
            Assert.Throws<InvalidOperationException>(
                () => _blockChain.Append(
                    blockWithAlreadyUsedIndex,
                    TestUtils.CreateBlockCommit(blockWithAlreadyUsedIndex)));

            Block blockWithIndexAfterNonexistentIndex = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = prev.Height + 2,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = prev.BlockHash,
                        LastCommit = TestUtils.CreateBlockCommit(prev.BlockHash, prev.Height + 1, 0),
                    },
                },
                _fx.Proposer);
            Assert.Throws<InvalidOperationException>(
                () => _blockChain.Append(
                    blockWithIndexAfterNonexistentIndex,
                    TestUtils.CreateBlockCommit(blockWithIndexAfterNonexistentIndex)));
        }

        [Fact]
        public void ValidateNextBlockInvalidPreviousHash()
        {
            _blockChain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext));

            Block invalidPreviousHashBlock = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 2,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        // Should be _validNext.Hash instead
                        PreviousHash = _validNext.PreviousHash,
                        // ReSharper disable once PossibleInvalidOperationException
                        LastCommit = TestUtils.CreateBlockCommit(
                            _validNext.PreviousHash, 1, 0),
                    },
                },
                _fx.Proposer);
            Assert.Throws<InvalidOperationException>(() =>
                    _blockChain.Append(
                        invalidPreviousHashBlock,
                        TestUtils.CreateBlockCommit(invalidPreviousHashBlock)));
        }

        [Fact]
        public void ValidateNextBlockInvalidTimestamp()
        {
            _blockChain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext));

            Block invalidPreviousTimestamp = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 2,
                        Timestamp = _validNext.Timestamp.AddSeconds(-1),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _validNext.BlockHash,
                        LastCommit = TestUtils.CreateBlockCommit(_validNext),
                    },
                },
                _fx.Proposer);
            Assert.Throws<InvalidOperationException>(() =>
                    _blockChain.Append(
                        invalidPreviousTimestamp,
                        TestUtils.CreateBlockCommit(invalidPreviousTimestamp)));
        }

        [Fact]
        public void ValidateNextBlockInvalidStateRootHash()
        {
            var options = new BlockChainOptions
            {
                BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
            };
            var genesisBlock = TestUtils.ProposeGenesisBlock(
                TestUtils.ProposeGenesis(TestUtils.GenesisProposer.PublicKey),
                TestUtils.GenesisProposer);

            var chain1 = new BlockChain(genesisBlock, options);
            var endBlockActions = new IAction[]
            {
                new SetStatesAtBlock(default, "foo", default, 0),
            }.ToImmutableArray();
            var options2 = new BlockChainOptions
            {
                PolicyActions = new PolicyActions
                {
                    EndBlockActions = [new SetStatesAtBlock(default, "foo", default, 0)],
                },
                BlockInterval = options.BlockInterval,
            };
            var chain2 = new BlockChain(genesisBlock, options2);

            Block block1 = chain1.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Version = BlockHeader.CurrentProtocolVersion,
                        Height = 1,
                        Timestamp = genesisBlock.Timestamp.AddSeconds(1),
                        Proposer = TestUtils.GenesisProposer.Address,
                        PreviousHash = genesisBlock.BlockHash,
                    },
                },
                TestUtils.GenesisProposer);

            Assert.Throws<InvalidOperationException>(() =>
                chain2.Append(block1, TestUtils.CreateBlockCommit(block1)));

            chain1.Append(block1, TestUtils.CreateBlockCommit(block1));
        }

        [Fact]
        public void ValidateNextBlockInvalidStateRootHashBeforePostpone()
        {
            var beforePostponeBPV = BlockHeader.CurrentProtocolVersion;
            var options1 = new BlockChainOptions
            {
                BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
            };
            var actionEvaluator = new ActionEvaluator(
                options1.Repository.StateStore,
                options1.PolicyActions);
            var preGenesis = TestUtils.ProposeGenesis(
                proposer: TestUtils.GenesisProposer.PublicKey,
                protocolVersion: beforePostponeBPV);
            var genesisBlock = preGenesis.Sign(
                TestUtils.GenesisProposer,
                actionEvaluator.Evaluate(preGenesis, default)[^1].OutputWorld.Trie.Hash);
            var chain1 = new BlockChain(genesisBlock, options1);

            Block block1 = chain1.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Version = beforePostponeBPV,
                        Height = 1,
                        Timestamp = genesisBlock.Timestamp.AddSeconds(1),
                        Proposer = TestUtils.GenesisProposer.Address,
                        PreviousHash = genesisBlock.BlockHash,
                    },
                },
                TestUtils.GenesisProposer);

            var policyWithBlockAction = new BlockChainOptions
            {
                PolicyActions = new PolicyActions
                {
                    BeginBlockActions = [],
                    EndBlockActions = [new SetStatesAtBlock(default, "foo", default, 1)],
                },
                BlockInterval = options1.BlockInterval,
            };
            var chain2 = new BlockChain(genesisBlock, policyWithBlockAction);

            Assert.Throws<InvalidOperationException>(() =>
                chain2.Append(block1, TestUtils.CreateBlockCommit(block1)));

            chain1.Append(block1, TestUtils.CreateBlockCommit(block1));
        }

        [Fact]
        public void ValidateNextBlockInvalidStateRootHashOnPostpone()
        {
            var beforePostponeBPV = BlockHeader.CurrentProtocolVersion;
            var options = new BlockChainOptions
            {
                PolicyActions = new PolicyActions
                {
                    BeginBlockActions = [new SetStatesAtBlock(default, "foo", default, 1)],
                },
                BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
            };
            var actionEvaluator = new ActionEvaluator(
                options.Repository.StateStore,
                options.PolicyActions);
            var rawGenesis = TestUtils.ProposeGenesis(
                proposer: TestUtils.GenesisProposer.PublicKey,
                protocolVersion: beforePostponeBPV);
            var genesisBlock = rawGenesis.Sign(
                TestUtils.GenesisProposer,
                actionEvaluator.Evaluate(rawGenesis, default)[^1].OutputWorld.Trie.Hash);
            var chain = new BlockChain(genesisBlock, options);

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
            Block block1 = chain.EvaluateAndSign(
                preBlock1,
                TestUtils.GenesisProposer);
            Assert.Equal(genesisBlock.StateRootHash, block1.StateRootHash);

            Block block2 = preBlock1.Sign(
                TestUtils.GenesisProposer,
                actionEvaluator.Evaluate(preBlock1, genesisBlock.StateRootHash)[^1].OutputWorld.Trie.Hash);

            Assert.Throws<InvalidOperationException>(() =>
                chain.Append(block2, TestUtils.CreateBlockCommit(block2)));

            chain.Append(block1, TestUtils.CreateBlockCommit(block1));
        }

        [Fact]
        public void ValidateNextBlockLastCommitNullAtIndexOne()
        {
            Block validNextBlock = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);
            _blockChain.Append(validNextBlock, TestUtils.CreateBlockCommit(validNextBlock));
            Assert.Equal(_blockChain.Tip, validNextBlock);
        }

        [Fact]
        public void ValidateNextBlockLastCommitUpperIndexOne()
        {
            Block block1 = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);
            _blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));

            var blockCommit = TestUtils.CreateBlockCommit(block1);
            Block block2 = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 2,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = block1.BlockHash,
                        LastCommit = blockCommit,
                    },
                },
                _fx.Proposer);
            _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));
            Assert.Equal(_blockChain.Tip, block2);
        }

        [Fact]
        public void ValidateNextBlockLastCommitFailsUnexpectedValidator()
        {
            Block block1 = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);
            _blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));

            var invalidValidator = new PrivateKey();
            var validators = TestUtils.ValidatorPrivateKeys.Append(invalidValidator).ToList();
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
                Flag = VoteFlag.PreCommit,
            }.Sign(validators[index])).ToImmutableArray();
            var blockCommit = new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = block1.BlockHash,
                Votes = votes,
            };

            Block block2 = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 2,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = block1.BlockHash,
                        LastCommit = blockCommit,
                    },
                },
                _fx.Proposer);
            Assert.Throws<InvalidOperationException>(() =>
                _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2)));
        }

        [Fact]
        public void ValidateNextBlockLastCommitFailsDropExpectedValidator()
        {
            Block block1 = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);
            _blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));

            var keysExceptPeer0 = TestUtils.ValidatorPrivateKeys.Where(
                key => key != TestUtils.ValidatorPrivateKeys[0]).ToList();
            var votes = keysExceptPeer0.Select(key => new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block1.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = key.Address,
                ValidatorPower = TestUtils.Validators.GetValidator(key.Address).Power,
                Flag = VoteFlag.PreCommit,
            }.Sign(key)).ToImmutableArray();
            var blockCommit = new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = block1.BlockHash,
                Votes = votes,
            };
            Block block2 = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 2,
                        Timestamp = DateTimeOffset.UtcNow,
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = block1.BlockHash,
                        LastCommit = blockCommit,
                    },
                },
                _fx.Proposer);
            Assert.Throws<InvalidOperationException>(() =>
                _blockChain.Append(block2, TestUtils.CreateBlockCommit(block2)));
        }

        [Fact]
        public void ValidateBlockCommitGenesis()
        {
            // Works fine.
            _blockChain.ValidateBlockCommit(_fx.GenesisBlock, default);

            // Should be null for genesis.
            Assert.Throws<InvalidOperationException>(() => _blockChain.ValidateBlockCommit(
                _fx.GenesisBlock,
                new BlockCommit
                {
                    Height = 0,
                    Round = 0,
                    BlockHash = _fx.GenesisBlock.BlockHash,
                    Votes = [.. TestUtils.ValidatorPrivateKeys.Select(x => new VoteMetadata
                    {
                        Height = 0,
                        Round = 0,
                        BlockHash = _fx.GenesisBlock.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = x.Address,
                        ValidatorPower = TestUtils.Validators.GetValidator(x.Address).Power,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(x))],
                }));
        }

        [Fact]
        public void ValidateBlockCommitFailsDifferentBlockHash()
        {
            Block validNextBlock = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);

            Assert.Throws<InvalidOperationException>(() =>
                _blockChain.Append(
                    validNextBlock,
                    TestUtils.CreateBlockCommit(
                        new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                        1,
                        0)));
        }

        [Fact]
        public void ValidateBlockCommitFailsDifferentHeight()
        {
            Block validNextBlock = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);

            Assert.Throws<InvalidOperationException>(() =>
                _blockChain.Append(
                    validNextBlock,
                    TestUtils.CreateBlockCommit(
                        validNextBlock.BlockHash,
                        2,
                        0)));
        }

        [Fact]
        public void ValidateBlockCommitFailsDifferentValidatorSet()
        {
            Block validNextBlock = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);

            Assert.Throws<InvalidOperationException>(() =>
                _blockChain.Append(
                    validNextBlock,
                    new BlockCommit
                    {
                        Height = 1,
                        Round = 0,
                        BlockHash = validNextBlock.BlockHash,
                        Votes = [.. Enumerable.Range(0, TestUtils.Validators.Count)
                            .Select(x => new PrivateKey())
                            .Select(x => new VoteMetadata
                            {
                                Height = 1,
                                Round = 0,
                                BlockHash = validNextBlock.BlockHash,
                                Timestamp = DateTimeOffset.UtcNow,
                                Validator = x.Address,
                                ValidatorPower = BigInteger.One,
                                Flag = VoteFlag.PreCommit,
                            }.Sign(x))],
                    }));
        }

        [Fact]
        public void ValidateBlockCommitFailsNullBlockCommit()
        {
            Block validNextBlock = _blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = _fx.GenesisBlock.Timestamp.AddDays(1),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = _fx.GenesisBlock.BlockHash,
                    },
                },
                _fx.Proposer);

            Assert.Throws<InvalidOperationException>(() =>
                _blockChain.Append(validNextBlock, default));
        }

        [Fact]
        public void ValidateBlockCommitFailsInsufficientPower()
        {
            var privateKey1 = new PrivateKey();
            var privateKey2 = new PrivateKey();
            var privateKey3 = new PrivateKey();
            var privateKey4 = new PrivateKey();
            var validator1 = Validator.Create(privateKey1.Address, 10);
            var validator2 = Validator.Create(privateKey2.Address, 1);
            var validator3 = Validator.Create(privateKey3.Address, 1);
            var validator4 = Validator.Create(privateKey4.Address, 1);
            var validatorSet = ImmutableSortedSet.Create(
                [validator1, validator2, validator3, validator4]);
            BlockChain blockChain = TestUtils.MakeBlockChain(
                validatorSet: validatorSet);
            Block validNextBlock = blockChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Height = 1,
                        Timestamp = blockChain.Genesis.Timestamp.AddDays(1),
                        Proposer = _fx.Proposer.Address,
                        PreviousHash = blockChain.Genesis.BlockHash,
                    },
                },
                _fx.Proposer);

            Vote GenerateVote(PrivateKey key, BigInteger power, VoteFlag flag)
            {
                var metadata = new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = validNextBlock.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = key.Address,
                    ValidatorPower = power,
                    Flag = flag,
                };
                return metadata.Sign(flag == VoteFlag.Null ? null : key);
            }

            ImmutableArray<Vote> GenerateVotes(
                VoteFlag flag1,
                VoteFlag flag2,
                VoteFlag flag3,
                VoteFlag flag4)
            {
                return new[]
                {
                    GenerateVote(privateKey1, validator1.Power, flag1),
                    GenerateVote(privateKey2, validator2.Power, flag2),
                    GenerateVote(privateKey3, validator3.Power, flag3),
                    GenerateVote(privateKey4, validator4.Power, flag4),
                }.OrderBy(vote => vote.Validator).ToImmutableArray();
            }

            var fullBlockCommit = new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = validNextBlock.BlockHash,
                Votes = GenerateVotes(
                    VoteFlag.PreCommit,
                    VoteFlag.PreCommit,
                    VoteFlag.PreCommit,
                    VoteFlag.PreCommit),
            };
            blockChain.ValidateBlockCommit(validNextBlock, fullBlockCommit);

            // Can propose if power is big enough even count condition is not met.
            var validBlockCommit = new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = validNextBlock.BlockHash,
                Votes = GenerateVotes(
                    VoteFlag.PreCommit,
                    VoteFlag.Null,
                    VoteFlag.Null,
                    VoteFlag.Null),
            };
            blockChain.ValidateBlockCommit(validNextBlock, validBlockCommit);

            // Can not propose if power isn't big enough even count condition is met.
            var invalidBlockCommit = new BlockCommit
            {
                Height = 1,
                Round = 0,
                BlockHash = validNextBlock.BlockHash,
                Votes = GenerateVotes(
                    VoteFlag.Null,
                    VoteFlag.PreCommit,
                    VoteFlag.PreCommit,
                    VoteFlag.PreCommit),
            };
            Assert.Throws<InvalidOperationException>(() =>
                blockChain.ValidateBlockCommit(validNextBlock, invalidBlockCommit));
        }

        [Fact]
        public void ValidateNextBlockOnChainRestart()
        {
            var newChain = new BlockChain(_blockChain.Genesis, _blockChain.Options);
            newChain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext));
            Assert.Equal(newChain.Tip, _validNext);
        }

        [Fact]
        public void ValidateNextBlockAEVChangedOnChainRestart()
        {
            var endBlockActions =
                new IAction[] { new SetStatesAtBlock(default, "foo", default, 0), }
                    .ToImmutableArray();
            var policyWithBlockAction = new BlockChainOptions
            {
                PolicyActions = new PolicyActions
                {
                    EndBlockActions = endBlockActions,
                },
            };

            var newChain = new BlockChain(_blockChain.Genesis, policyWithBlockAction);

            Block newValidNext = newChain.EvaluateAndSign(
                new RawBlock
                {
                    Header = new BlockHeader
                    {
                        Version = BlockHeader.CurrentProtocolVersion,
                        Height = newChain.Tip.Height + 1,
                        Timestamp = newChain.Tip.Timestamp.AddSeconds(1),
                        Proposer = TestUtils.GenesisProposer.Address,
                        PreviousHash = newChain.Tip.BlockHash,
                    },
                },
                TestUtils.GenesisProposer);

            Assert.NotEqual(_validNext, newValidNext);

            Assert.Throws<InvalidOperationException>(() =>
                newChain.Append(_validNext, TestUtils.CreateBlockCommit(_validNext)));

            newChain.Append(newValidNext, TestUtils.CreateBlockCommit(newValidNext));
        }
    }
}
