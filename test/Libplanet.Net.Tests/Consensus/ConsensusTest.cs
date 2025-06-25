using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Nito.AsyncEx;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ConsensusTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task StartAsProposer()
    {
        var blockProposedEvent = new ManualResetEvent(false);
        var preVoteEnteredEvent = new ManualResetEvent(false);
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockChain();
        await using var consensus = TestUtils.CreateConsensus(blockchain);
        using var _1 = consensus.PreVoteEntered.Subscribe(_ =>
        {
            preVoteEnteredEvent.Set();
        });
        using var _2 = consensus.BlockProposed.Subscribe(_ =>
        {
            blockProposedEvent.Set();
        });

        await consensus.StartAsync(default);
        Assert.True(blockProposedEvent.WaitOne(1000), "Block proposal did not happen in time.");
        Assert.True(preVoteEnteredEvent.WaitOne(10000), "PreVote step did not happen in time.");

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task StartAsProposerWithLastCommit()
    {
        Block? proposedBlock = null;
        var preVoteEnteredEvent = new ManualResetEvent(false);
        var blockProposedEvent = new ManualResetEvent(false);
        var blockchain = TestUtils.CreateBlockChain();
        var block1 = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var previousCommit = TestUtils.CreateBlockCommit(block1);
        blockchain.Append(block1, previousCommit);

        await using var consensus = TestUtils.CreateConsensus(
            blockchain,
            height: 2,
            previousCommit: new BlockCommit
            {
                Votes = previousCommit.Votes,
            },
            privateKey: TestUtils.PrivateKeys[2],
            validators: Libplanet.Tests.TestUtils.Validators);

        using var _1 = consensus.PreVoteEntered.Subscribe(state =>
        {
            preVoteEnteredEvent.Set();
        });
        using var _2 = consensus.BlockProposed.Subscribe(e =>
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
        var stepChangedEvent = new ManualResetEvent(false);
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
        var preVoteEnteredEvent = new ManualResetEvent(false);
        var endCommitEnteredEvent = new ManualResetEvent(false);
        var blockProposedEvent = new ManualResetEvent(false);

        // Add block #1 so we can start with a last commit for height 2.
        var blockchain = TestUtils.CreateBlockChain();
        var block1 = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var previousCommit = TestUtils.CreateBlockCommit(block1);
        blockchain.Append(block1, previousCommit);

        await using var consensus = TestUtils.CreateConsensus(
            blockchain,
            height: 2,
            previousCommit: previousCommit,
            privateKey: TestUtils.PrivateKeys[2],
            validators: TestUtils.Validators);

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
        using var _2 = consensus.BlockProposed.Subscribe(e =>
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
            consensus.PostVote(vote);
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
            consensus.PostVote(vote);
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
        consensus.PostVote(vote2);

        await Task.Delay(100);  // Wait for the new message to be added to the message log.
        blockCommit = consensus.GetBlockCommit();
        Assert.Equal(4, blockCommit.Votes.Where(vote => vote.Type == VoteType.PreCommit).Count());
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowOnInvalidProposerMessage()
    {
        Exception? exceptionThrown = null;
        var exceptionOccurredEvent = new ManualResetEvent(false);

        var blockchain = TestUtils.CreateBlockChain();
        await using var consensus = TestUtils.CreateConsensus(blockchain);
        using var _ = consensus.ExceptionOccurred.Subscribe(e =>
        {
            exceptionThrown = e;
            exceptionOccurredEvent.Set();
        });
        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[0]);
        var proposal = new ProposalMetadata
        {
            BlockHash = block.BlockHash,
            Height = block.Height,
            Round = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = TestUtils.PrivateKeys[0].Address,
            ValidRound = -1,
        }.Sign(TestUtils.PrivateKeys[0], block);

        await consensus.StartAsync(default);

        consensus.PostProposal(proposal);
        Assert.True(exceptionOccurredEvent.WaitOne(1000), "Exception did not occur in time.");
        Assert.IsType<ArgumentException>(exceptionThrown);
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowOnDifferentHeightMessage()
    {
        Exception? exceptionThrown = null;
        var exceptionOccurredEvent = new ManualResetEvent(false);

        var blockchain = TestUtils.CreateBlockChain();
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
        consensus.PostProposal(proposal);
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
        consensus.PostVote(vote);
        Assert.True(exceptionOccurredEvent.WaitOne(1000), "Exception did not occur in time.");
        Assert.IsType<InvalidOperationException>(exceptionThrown);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanPreCommitOnEndCommit()
    {
        var enteredPreVote = new AsyncAutoResetEvent();
        var enteredPreCommit = new AsyncAutoResetEvent();
        var enteredEndCommit = new AsyncAutoResetEvent();
        var blockHeightOneAppended = new AsyncAutoResetEvent();
        var enteredHeightTwo = new AsyncAutoResetEvent();

        TimeSpan newHeightDelay = TimeSpan.FromSeconds(1);

        var options = new BlockchainOptions
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
        var fx = new MemoryRepositoryFixture(options);
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockChain(options);

        Net.Consensus.Consensus consensus = new Net.Consensus.Consensus(
            blockchain,
            1,
            TestUtils.PrivateKeys[0].AsSigner(),
            options: new ConsensusOptions());
        // using var _1 = consensus.MessagePublished.Subscribe(consensus.ProduceMessage);

        // using var _2 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step == ConsensusStep.PreVote)
        //     {
        //         enteredPreVote.Set();
        //     }

        //     if (state.Step == ConsensusStep.PreCommit)
        //     {
        //         enteredPreCommit.Set();
        //     }

        //     if (state.Step == ConsensusStep.EndCommit)
        //     {
        //         enteredEndCommit.Set();
        //     }
        // });

        using var _3 = blockchain.TipChanged.Subscribe(eventArgs =>
        {
            if (eventArgs.Tip.Height == 1L)
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

        await consensus.StartAsync(default);
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(block, TestUtils.PrivateKeys[1]));

        foreach (int i in new int[] { 1, 2, 3 })
        {
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = new VoteMetadata
                    {
                        Height = 1,
                        Round = 0,
                        BlockHash = block.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = TestUtils.PrivateKeys[i].Address,
                        ValidatorPower = TestUtils.Validators[i].Power,
                        Type = VoteType.PreVote,
                    }.Sign(TestUtils.PrivateKeys[i])
                });
        }

        // Two additional votes should be enough to reach a consensus.
        foreach (int i in new int[] { 1, 2 })
        {
            consensus.ProduceMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = new VoteMetadata
                    {
                        Height = 1,
                        Round = 0,
                        BlockHash = block.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = TestUtils.PrivateKeys[i].Address,
                        ValidatorPower = TestUtils.Validators[i].Power,
                        Type = VoteType.PreCommit,
                    }.Sign(TestUtils.PrivateKeys[i])
                });
        }

        await blockHeightOneAppended.WaitAsync();
        Assert.Equal(
            3,
            consensus.GetBlockCommit().Votes.Count(vote => vote.Type == VoteType.PreCommit));

        Assert.True(enteredPreVote.IsSet);
        Assert.True(enteredPreCommit.IsSet);
        Assert.True(enteredEndCommit.IsSet);

        // Add the last vote and wait for it to be consumed.
        consensus.ProduceMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = block.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = TestUtils.PrivateKeys[3].Address,
                    ValidatorPower = BigInteger.One,
                    Type = VoteType.PreCommit,
                }.Sign(TestUtils.PrivateKeys[3])
            });
        Thread.Sleep(10);
        Assert.Equal(
            4,
            consensus.GetBlockCommit()!.Votes.Count(vote => vote.Type == VoteType.PreCommit));
    }

    /// <summary>
    /// <para>
    /// This test tests whether a validator can discard received proposal
    /// when another proposal has +2/3 votes and maj23 information.
    /// This Can be happen in following scenario.
    /// </para>
    /// <para>
    /// There exists 4 validators A B C and D, where D is attacker.
    /// <list type="bullet">
    /// <item><description>
    ///     Validator D sends the block X's proposal to validator A, and block Y's proposal to
    ///     validator B and C, both blocks are valid.
    /// </description></item>
    /// <item><description>
    ///     The validator A will broadcast block X's pre-vote and the validator C and D
    ///     will broadcast block Y's pre-vote.
    /// </description></item>
    /// <item><description>
    ///     The validator D sends block X's pre-vote to the validator A and B,
    ///     and sends block Y's pre-vote to the validator C.
    /// </description></item>
    /// <item><description>
    ///     The validator C will lock block Y and change its state to pre-commit state
    ///     since 2/3+ pre-vote messages are collected.
    /// </description></item>
    ///     Round is increased and other validator proposes valid block, but there are no
    ///     2/3+ validator to vote to the new valid block since 1/3 of them are locked in
    ///     block Y.
    /// <item><description>
    /// </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// So this test make one single candidate which is validator A in scenario above,
    /// to check the validator A can replace its proposal from block X to block Y when
    /// receiving <see cref="ConsensusMaj23Message"/> message from peer C or D.
    /// </para>
    /// </summary>
    [Fact(Timeout = Timeout)]
    public async Task CanReplaceProposal()
    {
        var privateKeys = Enumerable.Range(0, 4).Select(_ => new PrivateKey()).ToArray();
        // Order keys as validator set's order to run test as intended.
        privateKeys = privateKeys.OrderBy(key => key.Address).ToArray();
        var proposer = privateKeys[1];
        var key1 = privateKeys[2];
        var key2 = privateKeys[3];
        BigInteger proposerPower = TestUtils.Validators[1].Power;
        BigInteger power1 = TestUtils.Validators[2].Power;
        BigInteger power2 = TestUtils.Validators[3].Power;
        var stepChanged = new AsyncAutoResetEvent();
        var proposalModified = new AsyncAutoResetEvent();
        var prevStep = ConsensusStep.Default;
        BlockHash? prevProposal = null;
        var validators = ImmutableSortedSet.Create(
        [
            new Validator { Address = privateKeys[0].Address },
            new Validator { Address = proposer.Address },
            new Validator { Address = key1.Address },
            new Validator { Address = key2.Address },
        ]);

        var blockchain = TestUtils.CreateBlockChain();
        await using var consensus = TestUtils.CreateConsensus(
            blockchain: blockchain,
            privateKey: privateKeys[0],
            validators: validators);
        var blockA = blockchain.ProposeBlock(proposer);
        var blockB = blockchain.ProposeBlock(proposer);
        // using var _0 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step != prevStep)
        //     {
        //         prevStep = state.Step;
        //         stepChanged.Set();
        //     }

        //     if (!state.Proposal.Equals(prevProposal))
        //     {
        //         prevProposal = state.Proposal;
        //         proposalModified.Set();
        //     }
        // });
        await consensus.StartAsync(default);
        await stepChanged.WaitAsync();
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
        var preVoteA2 = new ConsensusPreVoteMessage
        {
            PreVote = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = blockA.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = key2.Address,
                ValidatorPower = power2,
                Type = VoteType.PreVote,
            }.Sign(key2)
        };
        var proposalB = new ProposalMetadata
        {
            BlockHash = blockB.BlockHash,
            Height = 1,
            Round = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            ValidRound = -1,
        }.Sign(proposer, blockB);
        var proposalAMsg = new ConsensusProposalMessage { Proposal = proposalA };
        var proposalBMsg = new ConsensusProposalMessage { Proposal = proposalB };
        consensus.ProduceMessage(proposalAMsg);
        await proposalModified.WaitAsync();
        Assert.Equal(proposalA, consensus.Proposal);

        // Proposal B is ignored because proposal A is received first.
        consensus.ProduceMessage(proposalBMsg);
        Assert.Equal(proposalA, consensus.Proposal);
        consensus.ProduceMessage(preVoteA2);

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

        var preVoteB0 = new ConsensusPreVoteMessage
        {
            PreVote = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = blockB.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = proposer.Address,
                ValidatorPower = proposerPower,
                Type = VoteType.PreVote,
            }.Sign(proposer)
        };
        var preVoteB1 = new ConsensusPreVoteMessage
        {
            PreVote = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = blockB.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = key1.Address,
                ValidatorPower = power1,
                Type = VoteType.PreVote,
            }.Sign(key1)
        };
        var preVoteB2 = new ConsensusPreVoteMessage
        {
            PreVote = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = blockB.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = key2.Address,
                ValidatorPower = power2,
                Type = VoteType.PreVote,
            }.Sign(key2)
        };
        consensus.ProduceMessage(preVoteB0);
        consensus.ProduceMessage(preVoteB1);
        consensus.ProduceMessage(preVoteB2);
        await proposalModified.WaitAsync();
        Assert.Null(consensus.Proposal);
        consensus.ProduceMessage(proposalBMsg);
        await proposalModified.WaitAsync();
        Assert.Equal(
consensus.Proposal,
proposalBMsg.Proposal);
        await stepChanged.WaitAsync();
        await stepChanged.WaitAsync();
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        Assert.Equal(
blockB.BlockHash.ToString(),
JsonSerializer.Deserialize<ContextJson>(consensus.ToString()).valid_value);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanCreateContextWithLastingEvaluation()
    {
        var onTipChanged = new AsyncAutoResetEvent();
        var enteredHeightTwo = new AsyncAutoResetEvent();

        TimeSpan newHeightDelay = TimeSpan.FromMilliseconds(100);
        int actionDelay = 2000;

        var fx = new MemoryRepositoryFixture();
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockChain(fx.Options);


        var consensusContext = new ConsensusReactor(
            null,
            blockchain,
            new ConsensusReactorOptions { PrivateKey = new PrivateKey() });
        Net.Consensus.Consensus consensus = consensusContext.CurrentContext;
        // using var _1 = consensus.MessagePublished.Subscribe(consensus.ProduceMessage);

        using var _2 = blockchain.TipChanged.Subscribe(eventArgs =>
        {
            if (eventArgs.Tip.Height == 1L)
            {
                onTipChanged.Set();
            }
        });

        // consensusContext.StateChanged += (_, eventArgs) =>
        // {
        //     if (consensusContext.Height == 2L)
        //     {
        //         enteredHeightTwo.Set();
        //     }
        // };

        var action = new DelayAction(actionDelay);
        var tx = new TransactionMetadata
        {
            Signer = TestUtils.PrivateKeys[1].Address,
            GenesisHash = blockchain.Genesis.BlockHash,
            Actions = new[] { action }.ToBytecodes(),
        }.Sign(TestUtils.PrivateKeys[1]);
        blockchain.StagedTransactions.Add(tx);
        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);

        await consensusContext.StartAsync(default);
        consensus.ProduceMessage(
            TestUtils.CreateConsensusPropose(block, TestUtils.PrivateKeys[1]));

        foreach (int i in new int[] { 1, 2, 3 })
        {
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = new VoteMetadata
                    {
                        Height = 1,
                        Round = 0,
                        BlockHash = block.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = TestUtils.PrivateKeys[i].Address,
                        ValidatorPower = TestUtils.Validators[i].Power,
                        Type = VoteType.PreVote,
                    }.Sign(TestUtils.PrivateKeys[i])
                });
        }

        foreach (int i in new int[] { 1, 2, 3 })
        {
            consensus.ProduceMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = new VoteMetadata
                    {
                        Height = 1,
                        Round = 0,
                        BlockHash = block.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = TestUtils.PrivateKeys[i].Address,
                        ValidatorPower = TestUtils.Validators[i].Power,
                        Type = VoteType.PreCommit,
                    }.Sign(TestUtils.PrivateKeys[i])
                });
        }

        Assert.Equal(1, consensusContext.Height);
        var watch = Stopwatch.StartNew();
        await onTipChanged.WaitAsync();
        Assert.True(watch.ElapsedMilliseconds < (actionDelay * 0.5));
        watch.Restart();

        await enteredHeightTwo.WaitAsync();
        Assert.Equal(
            4,
            consensus.GetBlockCommit()!.Votes.Count(
                vote => vote.Type.Equals(VoteType.PreCommit)));
        Assert.True(watch.ElapsedMilliseconds > (actionDelay * 0.5));
        Assert.Equal(2, consensusContext.Height);
    }

    [Theory(Timeout = Timeout)]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task CanCollectPreVoteAfterMajority(int delay)
    {
        var stepChangedToPreVote = new AsyncAutoResetEvent();
        var stepChangedToPreCommit = new AsyncAutoResetEvent();
        Block? proposedBlock = null;
        int numPreVotes = 0;
        await using var consensus = TestUtils.CreateConsensus(
            consensusOptions: new ConsensusOptions
            {
                EnterPreCommitDelay = delay,
            });
        // using var _1 = consensus.StateChanged.Subscribe(state =>
        // {
        //     if (state.Step == ConsensusStep.PreVote)
        //     {
        //         stepChangedToPreVote.Set();
        //     }
        //     else if (state.Step == ConsensusStep.PreCommit)
        //     {
        //         stepChangedToPreCommit.Set();
        //     }
        // });
        // using var _2 = consensus.MessagePublished.Subscribe(message =>
        // {
        //     if (message is ConsensusProposalMessage proposalMsg)
        //     {
        //         proposedBlock = proposalMsg.Proposal.Block;
        //     }
        // });
        // consensus.VoteSetModified += (_, tuple) =>
        // {
        //     if (tuple.Flag == VoteType.PreVote)
        //     {
        //         numPreVotes = tuple.Votes.Count();
        //     }
        // };
        await consensus.StartAsync(default);
        await stepChangedToPreVote.WaitAsync();
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        if (proposedBlock is not { } block)
        {
            throw new XunitException("No proposal is made");
        }

        for (int i = 0; i < 3; i++)
        {
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = new VoteMetadata
                    {
                        Height = block.Height,
                        Round = 0,
                        BlockHash = block.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = TestUtils.PrivateKeys[i].Address,
                        ValidatorPower = TestUtils.Validators[i].Power,
                        Type = VoteType.PreVote,
                    }.Sign(TestUtils.PrivateKeys[i])
                });
        }

        // Send delayed PreVote message after sending preCommit message
        var cts = new CancellationTokenSource();
        const int preVoteDelay = 300;
        _ = Task.Run(
            async () =>
            {
                await Task.Delay(preVoteDelay, cts.Token);
                consensus.ProduceMessage(
                    new ConsensusPreVoteMessage
                    {
                        PreVote = new VoteMetadata
                        {
                            Height = block.Height,
                            Round = 0,
                            BlockHash = block.BlockHash,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = TestUtils.PrivateKeys[3].Address,
                            ValidatorPower = TestUtils.Validators[3].Power,
                            Type = VoteType.PreVote,
                        }.Sign(TestUtils.PrivateKeys[3])
                    });
            },
            cts.Token);

        await stepChangedToPreCommit.WaitAsync();
        cts.Cancel();
        Assert.Equal(delay < preVoteDelay ? 3 : 4, numPreVotes);
    }

    public struct ContextJson
    {
#pragma warning disable SA1300
#pragma warning disable IDE1006
        public string locked_value { get; set; }

        public int locked_round { get; set; }

        public string valid_value { get; set; }

        public int valid_round { get; set; }
#pragma warning restore IDE1006
#pragma warning restore SA1300
    }
}
