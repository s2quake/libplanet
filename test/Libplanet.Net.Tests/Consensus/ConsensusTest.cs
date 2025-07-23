using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Xunit.Abstractions;
using Xunit.Sdk;
using Libplanet.TestUtilities;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ConsensusTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task StartAsProposer()
    {
        using var blockProposeEvent = new ManualResetEvent(false);
        using var preVoteEvent = new ManualResetEvent(false);
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockchain();
        await using var consensus = TestUtils.CreateConsensus(blockchain);
        using var _1 = consensus.PreVote.Subscribe(_ =>
        {
            preVoteEvent.Set();
        });
        using var _2 = consensus.BlockPropose.Subscribe(_ =>
        {
            blockProposeEvent.Set();
        });

        await consensus.StartAsync(default);
        Assert.True(blockProposeEvent.WaitOne(1000), "Block proposal did not happen in time.");
        Assert.True(preVoteEvent.WaitOne(10000), "PreVote step did not happen in time.");

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task StartAsProposerWithLastCommit()
    {
        Block? proposedBlock = null;
        using var preVoteEnteredEvent = new ManualResetEvent(false);
        using var blockProposedEvent = new ManualResetEvent(false);
        var blockchain = TestUtils.CreateBlockchain();
        var block1 = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var previousCommit = TestUtils.CreateBlockCommit(block1);
        blockchain.Append(block1, previousCommit);

        await using var consensus = TestUtils.CreateConsensus(
            blockchain,
            height: 2,
            privateKey: TestUtils.PrivateKeys[2]);

        using var _1 = consensus.PreVote.Subscribe(state =>
        {
            preVoteEnteredEvent.Set();
        });
        using var _2 = consensus.BlockPropose.Subscribe(e =>
        {
            proposedBlock = e.Block;
            blockProposedEvent.Set();
        });

        await consensus.StartAsync(default);
        Assert.True(preVoteEnteredEvent.WaitOne(1000), "PreVote step did not happen in time.");
        Assert.True(blockProposedEvent.WaitOne(1000), "Block proposal did not happen in time.");

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.NotNull(proposedBlock);
        Assert.Equal(previousCommit, proposedBlock.PreviousCommit);
    }

    [Fact(Timeout = Timeout)]
    public async Task CannotStartTwice()
    {
        using var stepChangedEvent = new ManualResetEvent(false);
        await using var consensus = TestUtils.CreateConsensus();
        using var _ = consensus.StepChanged.Subscribe(step =>
        {
            if (step == ConsensusStep.Propose)
            {
                stepChangedEvent.Set();
            }
        });
        await consensus.StartAsync(default);

        Assert.True(stepChangedEvent.WaitOne(1000), "Consensus did not enter Propose step in time.");
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await consensus.StartAsync(default));
    }

    [Fact(Timeout = Timeout)]
    public async Task CanAcceptMessagesAfterCommitFailure()
    {
        Block? proposedBlock = null;
        using var preVoteEnteredEvent = new ManualResetEvent(false);
        using var endCommitEnteredEvent = new ManualResetEvent(false);
        using var blockProposedEvent = new ManualResetEvent(false);

        // Add block #1 so we can start with a last commit for height 2.
        var blockchain = TestUtils.CreateBlockchain();
        var block1 = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var previousCommit = TestUtils.CreateBlockCommit(block1);
        blockchain.Append(block1, previousCommit);

        await using var consensus = TestUtils.CreateConsensus(
            blockchain,
            height: 2,
            privateKey: TestUtils.PrivateKeys[2]);

        using var _1 = consensus.StepChanged.Subscribe(step =>
        {
            if (step == ConsensusStep.PreVote)
            {
                preVoteEnteredEvent.Set();
            }
            else if (step == ConsensusStep.EndCommit)
            {
                endCommitEnteredEvent.Set();
            }
        });
        using var _2 = consensus.BlockPropose.Subscribe(e =>
        {
            proposedBlock = e.Block;
            blockProposedEvent.Set();
        });

        await consensus.StartAsync(default);

        Assert.True(preVoteEnteredEvent.WaitOne(1000), "Consensus did not enter PreVote step in time.");
        Assert.True(blockProposedEvent.WaitOne(1000), "Consensus did not send proposal in time.");

        // Simulate bypass of consensus and block sync by swarm by
        // directly appending to the blockchain.
        Assert.NotNull(proposedBlock);
        blockchain.Append(proposedBlock!, TestUtils.CreateBlockCommit(proposedBlock!));
        Assert.Equal(2, blockchain.Tip.Height);

        // Make PreVotes to normally move to PreCommit step.
        foreach (var i in new int[] { 0, 1, 3 })
        {
            var vote = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = proposedBlock!.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.PrivateKeys[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreVote,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensus.Post(vote);
        }

        // Validator 2 will automatically vote its PreCommit.
        foreach (var i in new int[] { 0, 1 })
        {
            var vote = new VoteMetadata
            {
                Height = 2,
                Round = 0,
                BlockHash = proposedBlock!.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.PrivateKeys[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreCommit,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensus.Post(vote);
        }

        Assert.True(endCommitEnteredEvent.WaitOne(1000), "Consensus did not enter EndCommit step in time.");

        // Check consensus has only three votes.
        var blockCommit = consensus.GetBlockCommit();
        Assert.Equal(3, blockCommit.Votes.Where(vote => vote.Type == VoteType.PreCommit).Count());

        // Context should still accept new votes.
        var vote2 = new VoteMetadata
        {
            Height = 2,
            Round = 0,
            BlockHash = proposedBlock!.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[3].Address,
            ValidatorPower = TestUtils.Validators[3].Power,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[3]);
        consensus.Post(vote2);

        await Task.Delay(100);  // Wait for the new message to be added to the message log.
        blockCommit = consensus.GetBlockCommit();
        Assert.Equal(4, blockCommit.Votes.Where(vote => vote.Type == VoteType.PreCommit).Count());
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowOnInvalidProposerMessage()
    {
        Exception? exceptionThrown = null;
        using var exceptionOccurredEvent = new ManualResetEvent(false);

        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(blockchain, privateKey: TestUtils.PrivateKeys[0]);
        using var _ = consensus.ExceptionOccurred.Subscribe(e =>
        {
            exceptionThrown = e;
            exceptionOccurredEvent.Set();
        });
        var signer = TestUtils.PrivateKeys[0];
        var block = blockchain.ProposeBlock(signer);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 0,
        }.Create(signer);

        await consensus.StartAsync(default);

        consensus.Post(proposal);
        Assert.True(exceptionOccurredEvent.WaitOne(1000), "Exception did not occur in time.");
        var e = Assert.IsType<ArgumentException>(exceptionThrown);
        Assert.Equal("proposal", e.ParamName);
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowOnDifferentHeightMessage()
    {
        Exception? exceptionThrown = null;
        using var exceptionOccurredEvent = new ManualResetEvent(false);

        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(blockchain);
        using var _ = consensus.ExceptionOccurred.Subscribe(e =>
        {
            exceptionThrown = e;
            exceptionOccurredEvent.Set();
        });
        var signer = TestUtils.PrivateKeys[2].AsSigner();
        var block = new BlockBuilder
        {
            Height = 2,
        }.Create(signer);
        var proposal = new ProposalMetadata
        {
            BlockHash = block.BlockHash,
            Height = 2,
            Round = 2,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = signer.Address,
            ValidRound = -1,
        }.Sign(signer, block);

        await consensus.StartAsync(default);
        consensus.Post(proposal);
        Assert.True(exceptionOccurredEvent.WaitOne(1000), "Exception did not occur in time.");
        Assert.IsType<InvalidOperationException>(exceptionThrown);

        // Reset exception thrown.
        exceptionThrown = null;
        exceptionOccurredEvent.Reset();
        var vote = TestUtils.CreateVote(
            TestUtils.PrivateKeys[2],
            TestUtils.Validators[2].Power,
            2,
            0,
            block.BlockHash,
            VoteType.PreVote);
        consensus.Post(vote);
        Assert.True(exceptionOccurredEvent.WaitOne(1000), "Exception did not occur in time.");
        Assert.IsType<ArgumentException>(exceptionThrown);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanPreCommitOnEndCommit()
    {
        using var enteredPreVote = new ManualResetEvent(false);
        using var enteredPreCommit = new ManualResetEvent(false);
        using var enteredEndCommit = new ManualResetEvent(false);
        using var blockHeightOneAppended = new ManualResetEvent(false);

        var blockchainOptions = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MaxTransactionsBytes = 50 * 1024,
            },
        };
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockchain(blockchainOptions);
        await using var consensus = TestUtils.CreateConsensus(blockchain, 1, TestUtils.PrivateKeys[0]);

        using var _1 = consensus.StepChanged.Subscribe(step =>
        {
            if (step == ConsensusStep.PreVote)
            {
                enteredPreVote.Set();
            }

            if (step == ConsensusStep.PreCommit)
            {
                enteredPreCommit.Set();
            }

            if (step == ConsensusStep.EndCommit)
            {
                enteredEndCommit.Set();
            }
        });

        using var _2 = blockchain.TipChanged.Subscribe(eventArgs =>
        {
            if (eventArgs.Tip.Height == 1)
            {
                blockHeightOneAppended.Set();
            }
        });

        var action = new DelayAction(100);
        var tx = new TransactionMetadata
        {
            Signer = TestUtils.PrivateKeys[1].Address,
            GenesisHash = blockchain.Genesis.BlockHash,
            Actions = new[] { action }.ToBytecodes(),
        }.Sign(TestUtils.PrivateKeys[1]);
        blockchain.StagedTransactions.Add(tx);
        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 0,
        }.Create(TestUtils.PrivateKeys[1]);

        await consensus.StartAsync(default);
        consensus.Post(proposal);

        foreach (var i in new int[] { 1, 2, 3 })
        {
            var preVote = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.PrivateKeys[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreVote,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensus.Post(preVote);
        }

        // Two additional votes should be enough to reach a consensus.
        foreach (var i in new int[] { 1, 2 })
        {
            var preCommit = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.PrivateKeys[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreCommit,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensus.Post(preCommit);
        }

        Assert.True(blockHeightOneAppended.WaitOne(1000), "Block #1 was not appended in time.");
        Assert.Equal(
            3,
            consensus.GetBlockCommit().Votes.Count(vote => vote.Type == VoteType.PreCommit));

        Assert.True(enteredPreVote.WaitOne(1000), "PreVote step did not happen in time.");
        Assert.True(enteredPreCommit.WaitOne(1000), "PreCommit step did not happen in time.");
        Assert.True(enteredEndCommit.WaitOne(1000), "EndCommit step did not happen in time.");

        // Add the last vote and wait for it to be consumed.
        var vote = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[3].Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.PrivateKeys[3]);
        consensus.Post(vote);
        await Task.Delay(10);
        Assert.Equal(4, consensus.GetBlockCommit()!.Votes.Count(vote => vote.Type == VoteType.PreCommit));
    }

    [Fact(Timeout = Timeout)]
    public async Task CanReplaceProposal()
    {
        var random = RandomUtility.GetRandom(output);
        var privateKeys = Enumerable.Range(0, 4)
            .Select(_ => RandomUtility.PrivateKey(random))
            .OrderBy(key => key.Address)
            .ToArray();
        var proposer = privateKeys[1];
        var key1 = privateKeys[2];
        var key2 = privateKeys[3];
        var proposerPower = TestUtils.Validators[1].Power;
        var power1 = TestUtils.Validators[2].Power;
        var power2 = TestUtils.Validators[3].Power;
        using var stepChanged = new AutoResetEvent(false);
        using var proposalModified = new AutoResetEvent(false);
        Block? completedBlock = null;
        // var prevStep = ConsensusStep.Default;
        // BlockHash? prevProposal = null;
        var validators = ImmutableSortedSet.Create(
            new Validator { Address = privateKeys[0].Address },
            new Validator { Address = proposer.Address },
            new Validator { Address = key1.Address },
            new Validator { Address = key2.Address }
        );

        var blockchain = TestUtils.CreateBlockchain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: privateKeys[0],
            validators: validators);
        var blockA = blockchain.ProposeBlock(proposer);
        var blockB = blockchain.ProposeBlock(proposer);
        using var _0 = consensus.StepChanged.Subscribe(step =>
        {
            stepChanged.Set();
        });
        using var _1 = consensus.ProposalChanged.Subscribe(proposal =>
        {
            proposalModified.Set();
        });
        using var _2 = consensus.Completed.Subscribe(e =>
        {
            completedBlock = e.Block;
        });
        await consensus.StartAsync(default);
        Assert.True(stepChanged.WaitOne(1000), "Consensus step was not changed in time.");
        Assert.Equal(ConsensusStep.Propose, consensus.Step);

        var proposalA = new ProposalMetadata
        {
            BlockHash = blockA.BlockHash,
            Height = 1,
            Round = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            ValidRound = -1,
        }.Sign(proposer, blockA);
        var preVoteA2 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = blockA.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = key2.Address,
            ValidatorPower = power2,
            Type = VoteType.PreVote,
        }.Sign(key2);
        var proposalB = new ProposalMetadata
        {
            BlockHash = blockB.BlockHash,
            Height = 1,
            Round = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            ValidRound = -1,
        }.Sign(proposer, blockB);
        consensus.Post(proposalA);
        Assert.True(proposalModified.WaitOne(1000), "Proposal was not modified in time.");
        Assert.Equal(proposalA, consensus.Proposal);

        // Proposal B is ignored because proposal A is received first.
        consensus.Post(proposalB);
        Assert.Equal(proposalA, consensus.Proposal);
        consensus.Post(preVoteA2);

        // Validator 1 (key1) collected +2/3 pre-vote messages,
        // sends maj23 message to consensus.
        var maj23 = new Maj23Metadata
        {
            Height = 1,
            Round = 0,
            BlockHash = blockB.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = key1.Address,
            VoteType = VoteType.PreVote,
        }.Sign(key1);
        consensus.AddMaj23(maj23);

        var preVoteB0 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = blockB.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = proposer.Address,
            ValidatorPower = proposerPower,
            Type = VoteType.PreVote,
        }.Sign(proposer);
        var preVoteB1 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = blockB.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = key1.Address,
            ValidatorPower = power1,
            Type = VoteType.PreVote,
        }.Sign(key1);
        var preVoteB2 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = blockB.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = key2.Address,
            ValidatorPower = power2,
            Type = VoteType.PreVote,
        }.Sign(key2);
        consensus.Post(preVoteB0);
        consensus.Post(preVoteB1);
        consensus.Post(preVoteB2);
        Assert.True(proposalModified.WaitOne(1000), "Proposal was not modified in time.");
        Assert.Null(consensus.Proposal);
        consensus.Post(proposalB);
        Assert.True(proposalModified.WaitOne(1000), "Proposal was not modified in time.");
        Assert.Equal(consensus.Proposal, proposalB);
        Assert.True(stepChanged.WaitOne(1000), "Consensus step was not changed in time.");
        Assert.True(stepChanged.WaitOne(1000), "Consensus step was not changed in time.");
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        // Assert.Equal(blockB, completedBlock);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanCreateContextWithLastingEvaluation()
    {
        using var onTipChanged = new ManualResetEvent(false);
        using var enteredHeightTwo = new ManualResetEvent(false);

        TimeSpan newHeightDelay = TimeSpan.FromMilliseconds(100);
        int actionDelay = 2000;

        using var fx = new MemoryRepositoryFixture();
        var privateKey0 = TestUtils.PrivateKeys[0];
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockchain(fx.Options);
        await using var transport = TestUtils.CreateTransport(privateKey0);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = newHeightDelay,
        };
        var consensusService = new ConsensusService(
            privateKey0.AsSigner(),
            blockchain,
            options);
        // var consensus = consensusService.Consensus;

        using var _2 = blockchain.TipChanged.Subscribe(e =>
        {
            if (e.Tip.Height == 1L)
            {
                onTipChanged.Set();
            }
        });

        using var _3 = consensusService.HeightChanged.Subscribe(height =>
        {
            if (height == 2)
            {
                enteredHeightTwo.Set();
            }
        });

        var tx = new TransactionBuilder
        {
            GenesisHash = blockchain.Genesis.BlockHash,
            Actions = [new DelayAction(actionDelay)],
        }.Create(TestUtils.PrivateKeys[1], blockchain);
        blockchain.StagedTransactions.Add(tx);
        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 0,
        }.Create(TestUtils.PrivateKeys[1]);

        await consensusService.StartAsync(default);
        consensusService.Post(proposal);

        foreach (var i in new int[] { 1, 2, 3 })
        {
            var preVote = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.PrivateKeys[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreVote,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensusService.Post(preVote);
        }

        foreach (var i in new int[] { 1, 2, 3 })
        {
            var preCommit = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.PrivateKeys[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreCommit,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensusService.Post(preCommit);
        }

        Assert.Equal(1, consensusService.Height);
        var watch = Stopwatch.StartNew();
        Assert.True(onTipChanged.WaitOne(5000), "Tip was not changed in time.");
        Assert.True(watch.ElapsedMilliseconds < (actionDelay * 0.5));
        watch.Restart();

        Assert.True(enteredHeightTwo.WaitOne(5000), "Consensus did not enter height 2 in time.");
        // Assert.Equal(
        //     4,
        //     consensus.GetBlockCommit()!.Votes.Count(
        //         vote => vote.Type.Equals(VoteType.PreCommit)));
        Assert.True(watch.ElapsedMilliseconds > (actionDelay * 0.5));
        Assert.Equal(2, consensusService.Height);
    }

    [Theory(Timeout = Timeout)]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task CanCollectPreVoteAfterMajority(int delay)
    {
        using var preVoteStepEvent = new ManualResetEvent(false);
        using var preCommitStepEvent = new ManualResetEvent(false);
        Block? proposedBlock = null;
        int numPreVotes = 0;
        var options = new ConsensusOptions
        {
            EnterPreCommitDelay = delay,
        };
        await using var consensus = TestUtils.CreateConsensus(options: options);
        using var _1 = consensus.StepChanged.Subscribe(step =>
        {
            if (step == ConsensusStep.PreVote)
            {
                preVoteStepEvent.Set();
            }
            else if (step == ConsensusStep.PreCommit)
            {
                preCommitStepEvent.Set();
            }
        });
        using var _2 = consensus.BlockPropose.Subscribe(proposal =>
        {
            proposedBlock = proposal.Block;
        });
        using var _3 = consensus.VoteAdded.Subscribe(vote =>
        {
            if (vote.Type == VoteType.PreVote)
            {
                numPreVotes++;
            }
        });
        await consensus.StartAsync(default);
        Assert.True(preVoteStepEvent.WaitOne(1000), "Consensus did not enter PreVote step in time.");
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        if (proposedBlock is not { } block)
        {
            throw new XunitException("No proposal is made");
        }

        for (var i = 0; i < 3; i++)
        {
            var preVote = new VoteMetadata
            {
                Height = block.Height,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.PrivateKeys[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreVote,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensus.Post(preVote);
        }

        // Send delayed PreVote message after sending preCommit message
        using var cancellationToken = new CancellationTokenSource();
        const int preVoteDelay = 300;
        _ = Task.Run(
            async () =>
            {
                await Task.Delay(preVoteDelay, cancellationToken.Token);
                var preVote = new VoteMetadata
                {
                    Height = block.Height,
                    Round = 0,
                    BlockHash = block.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = TestUtils.PrivateKeys[3].Address,
                    ValidatorPower = TestUtils.Validators[3].Power,
                    Type = VoteType.PreVote,
                }.Sign(TestUtils.PrivateKeys[3]);
                consensus.Post(preVote);
            },
            cancellationToken.Token);

        Assert.True(preCommitStepEvent.WaitOne(1000), "Consensus did not enter PreCommit step in time.");
        await cancellationToken.CancelAsync();

        Assert.Equal(delay < preVoteDelay ? 3 : 4, numPreVotes);
    }
}
