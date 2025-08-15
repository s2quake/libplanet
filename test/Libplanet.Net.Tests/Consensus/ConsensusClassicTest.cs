using System.Diagnostics;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Xunit.Abstractions;
using Libplanet.TestUtilities;
using Libplanet.Extensions;
using Libplanet.Tests;
using System.Reactive.Linq;

using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ConsensusClassicTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task StartAsProposer()
    {
        var privateKey = PrivateKeys[1];
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(height: 1, Validators);
        using var observer = new ConsensusObserver(privateKey.AsSigner(), consensus, blockchain);

        consensus.StartAfter(100);

        var proposeTask = observer.ShouldPropose.WaitAsync();
        var proposal = await proposeTask.WaitAsync(TimeSpan.FromSeconds(3));
        var preVoteTask = observer.ShouldPreVote.WaitAsync();
        _ = consensus.ProposeAsync(proposal, default);
        await preVoteTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = Timeout)]
    public async Task StartAsProposerWithLastCommit()
    {
        var privateKey = PrivateKeys[2];
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(height: 2, Validators);
        using var observer = new ConsensusObserver(privateKey.AsSigner(), consensus, blockchain);
        var (_, blockCommit1) = blockchain.ProposeAndAppend(PrivateKeys[1]);

        consensus.StartAfter(100);
        var proposeTask = observer.ShouldPropose.WaitAsync();
        var proposal = await proposeTask.WaitAsync(TimeSpan.FromSeconds(3));;
        _ = consensus.ProposeAsync(proposal, default);
        var preVoteTask = observer.ShouldPreVote.WaitAsync();
        var preVote = await preVoteTask.WaitAsync(TimeSpan.FromSeconds(3));
        _ = consensus.PreVoteAsync(preVote, default);

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(blockCommit1, proposal.Block.PreviousCommit);
    }

    [Fact(Timeout = Timeout)]
    public async Task CannotStartTwice()
    {
        await using var consensus = new Net.Consensus.Consensus(height: 1, Validators);

        await consensus.StartAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => consensus.StartAsync(default));
    }

    [Fact(Timeout = Timeout)]
    public async Task CanAcceptMessagesAfterCommitFailure()
    {
        var blockchain = MakeBlockchain();
        blockchain.ProposeAndAppend(PrivateKeys[1]);

        await using var consensus = new Net.Consensus.Consensus(height: 2, Validators);
        var observer = new ConsensusObserver(Signers[2], consensus, blockchain);

        consensus.StartAfter(100);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreVote);
        var endCommitStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.EndCommit);
        var proposeTask = observer.ShouldPropose.WaitAsync();
        var proposal = await proposeTask.WaitAsync(TimeSpan.FromSeconds(2));

        _ = consensus.ProposeAsync(proposal, default);
        await preVoteStepTask.WaitAsync(TimeSpan.FromSeconds(10));

        blockchain.AppendWithBlockCommit(proposal.Block);

        Assert.Equal(2, blockchain.Tip.Height);

        // Make PreVotes to normally move to PreCommit step.
        foreach (var i in new int[] { 0, 1, 2, 3 })
        {
            var vote = new VoteBuilder
            {
                Validator = Validators[i],
                Height = 2,
                BlockHash = proposal.BlockHash,
                Type = VoteType.PreVote,
            }.Create(Signers[i]);
            _ = consensus.PreVoteAsync(vote, default);
        }

        // Validator 2 will automatically vote its PreCommit.
        foreach (var i in new int[] { 0, 1, 2 })
        {
            var vote = new VoteBuilder
            {
                Validator = Validators[i],
                Height = 2,
                BlockHash = proposal.BlockHash,
                Type = VoteType.PreCommit,
            }.Create(Signers[i]);
            _ = consensus.PreCommitAsync(vote, default);
        }

        await endCommitStepTask.WaitAsync(TimeSpan.FromSeconds(3));

        // Check consensus has only three votes.
        var preCommits = consensus.Round.PreCommits;
        var blockCommit = preCommits.GetBlockCommit();
        Assert.Equal(3, blockCommit.Votes.Where(vote => vote.Type == VoteType.PreCommit).Count());

        // Context should still accept new votes.
        var vote3 = new VoteBuilder
        {
            Validator = Validators[3],
            Height = 2,
            BlockHash = proposal!.BlockHash,
            Type = VoteType.PreCommit,
        }.Create(Signers[3]);

        await consensus.PreCommitAsync(vote3, default).WaitAsync(TimeSpan.FromSeconds(3));

        blockCommit = preCommits.GetBlockCommit();
        Assert.Equal(4, blockCommit.Votes.Where(vote => vote.Type == VoteType.PreCommit).Count());
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowOnInvalidProposerMessage()
    {
        var tcs = new TaskCompletionSource<Exception>();
        var blockchain = MakeBlockchain();
        await using var consensus = CreateConsensus();
        using var _ = consensus.ExceptionOccurred.Subscribe(tcs.SetResult);
        var signer = PrivateKeys[0];
        var block = blockchain.ProposeBlock(signer);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 0,
        }.Create(signer);

        await consensus.StartAsync(default);

        var e = await Assert.ThrowsAsync<ArgumentException>(() => consensus.ProposeAsync(proposal, default));
        Assert.Equal("proposal", e.ParamName);
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowOnDifferentHeightMessage()
    {
        var blockchain = MakeBlockchain();
        await using var consensus = CreateConsensus();
        using var controller = CreateConsensusController(
            consensus,
            PrivateKeys[1],
            blockchain);
        var signer = PrivateKeys[2].AsSigner();
        var block = new BlockBuilder
        {
            Height = 2,
        }.Create(signer);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 2,
            Timestamp = DateTimeOffset.UtcNow,
            ValidRound = -1,
        }.Create(signer);

        await consensus.StartAsync(default);
        await Assert.ThrowsAsync<ArgumentException>(() => consensus.ProposeAsync(proposal, default));

        var vote = new VoteBuilder
        {
            Validator = Validators[2],
            Height = 2,
            BlockHash = block.BlockHash,
            Type = VoteType.PreVote,
        }.Create(PrivateKeys[2]);

        await Assert.ThrowsAsync<ArgumentException>(() => consensus.PreVoteAsync(vote, default));
    }

    [Fact(Timeout = Timeout)]
    public async Task CanPreCommitOnEndCommit()
    {
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
        var blockchain = MakeBlockchain(blockchainOptions);
        await using var consensus = CreateConsensus();
        using var controller = CreateConsensusController(consensus, PrivateKeys[0], blockchain);
        using var _1 = consensus.Finalized.Subscribe(e => blockchain.Append(e.Block, e.BlockCommit));

        var preVoteStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreVote);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreCommit);
        var endCommitStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.EndCommit);

        var tipChangedTask = blockchain.TipChanged.WaitAsync(e => e.Tip.Height == 1);

        var action = new DelayAction(100);
        _ = blockchain.StagedTransactions.Add(PrivateKeys[1], new()
        {
            Actions = [action],
        });
        var block = blockchain.ProposeBlock(PrivateKeys[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 0,
        }.Create(PrivateKeys[1]);

        await consensus.StartAsync(default);
        _ = consensus.ProposeAsync(proposal, default);

        foreach (var i in new int[] { 1, 2, 3 })
        {
            var preVote = new VoteBuilder
            {
                Validator = Validators[i],
                Height = 1,
                BlockHash = block.BlockHash,
                Type = VoteType.PreVote,
            }.Create(PrivateKeys[i]);
            _ = consensus.PreVoteAsync(preVote, default);
        }

        // Two additional votes should be enough to reach a consensus.
        foreach (var i in new int[] { 1, 2 })
        {
            var preCommit = new VoteBuilder
            {
                Validator = Validators[i],
                Height = 1,
                BlockHash = block.BlockHash,
                Type = VoteType.PreCommit,
            }.Create(PrivateKeys[i]);
            _ = consensus.PreCommitAsync(preCommit, default);
        }

        await tipChangedTask.WaitAsync(TimeSpan.FromSeconds(100000));
        Assert.Equal(
            3,
            consensus.Round.PreCommits.GetBlockCommit().Votes.Count(vote => vote.Type == VoteType.PreCommit));

        await preVoteStepTask.WaitAsync(TimeSpan.FromSeconds(1));
        await preCommitStepTask.WaitAsync(TimeSpan.FromSeconds(1));
        await endCommitStepTask.WaitAsync(TimeSpan.FromSeconds(1));

        // Add the last vote and wait for it to be consumed.
        var vote = new VoteBuilder
        {
            Validator = Validators[3],
            Height = 1,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Type = VoteType.PreCommit,
        }.Create(PrivateKeys[3]);
        await consensus.PreCommitAsync(vote, default);
        Assert.Equal(4, consensus.Round.PreCommits.GetBlockCommit()!.Votes.Count(vote => vote.Type == VoteType.PreCommit));
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
        var proposerPower = Validators[1].Power;
        var power1 = Validators[2].Power;
        var power2 = Validators[3].Power;
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

        var blockchain = MakeBlockchain();
        await using var consensus = CreateConsensus(
            validators: validators);
        using var controller = CreateConsensusController(
            consensus,
            privateKeys[0],
            blockchain);
        var blockA = blockchain.ProposeBlock(proposer);
        var blockB = blockchain.ProposeBlock(proposer);
        using var _0 = consensus.StepChanged.Subscribe(step =>
        {
            stepChanged.Set();
        });
        using var _1 = consensus.Proposed.Subscribe(proposal =>
        {
            proposalModified.Set();
        });
        using var _2 = consensus.Finalized.Subscribe(e =>
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
        _ = consensus.ProposeAsync(proposalA, default);
        Assert.True(proposalModified.WaitOne(1000), "Proposal was not modified in time.");
        Assert.Equal(proposalA, consensus.Proposal);

        // Proposal B is ignored because proposal A is received first.
        _ = consensus.ProposeAsync(proposalB, default);
        var e1 = await consensus.ExceptionOccurred.WaitAsync().WaitAsync(TimeSpan.FromSeconds(1));
        var e2 = Assert.IsType<InvalidOperationException>(e1);
        Assert.StartsWith("Proposal already exists", e2.Message);

        Assert.Equal(proposalA, consensus.Proposal);
        _ = consensus.PreVoteAsync(preVoteA2, default);

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
        consensus.AddPreVoteMaj23(maj23);

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
        _ = consensus.PreVoteAsync(preVoteB0, default);
        _ = consensus.PreVoteAsync(preVoteB1, default);
        _ = consensus.PreVoteAsync(preVoteB2, default);
        await consensus.ProposalClaimed.WaitAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Null(consensus.Proposal);
        _ = consensus.ProposeAsync(proposalB, default);
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
        // using var onTipChanged = new ManualResetEvent(false);
        // using var enteredHeightTwo = new ManualResetEvent(false);

        TimeSpan newHeightDelay = TimeSpan.FromMilliseconds(100);
        int actionDelay = 2000;

        using var fx = new MemoryRepositoryFixture();
        var keyA = PrivateKeys[0];
        var blockchain = MakeBlockchain(fx.Options);
        await using var transport = CreateTransport(keyA);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = newHeightDelay,
        };
        var consensusService = new ConsensusService(keyA.AsSigner(), blockchain, transport, options);
        // var consensus = consensusService.Consensus;

        // using var _2 = blockchain.TipChanged.Subscribe(e =>
        // {
        //     if (e.Tip.Height == 1L)
        //     {
        //         onTipChanged.Set();
        //     }
        // });

        var tipChangedTask = blockchain.TipChanged.WaitAsync(e => e.Tip.Height == 1L);
        var heightChangedTask = consensusService.HeightChanged.WaitAsync(e => e == 2);

        // using var _3 = consensusService.HeightChanged.Subscribe(height =>
        // {
        //     if (height == 2)
        //     {
        //         enteredHeightTwo.Set();
        //     }
        // });

        var tx = new TransactionBuilder
        {
            GenesisHash = blockchain.Genesis.BlockHash,
            Actions = [new DelayAction(actionDelay)],
        }.Create(PrivateKeys[1], blockchain);
        blockchain.StagedTransactions.Add(tx);
        var block = blockchain.ProposeBlock(PrivateKeys[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 0,
        }.Create(PrivateKeys[1]);

        await transport.StartAsync(default);
        await consensusService.StartAsync(default);
        _ = consensusService.Consensus.ProposeAsync(proposal, default);

        foreach (var i in new int[] { 1, 2, 3 })
        {
            var preVote = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = PrivateKeys[i].Address,
                ValidatorPower = Validators[i].Power,
                Type = VoteType.PreVote,
            }.Sign(PrivateKeys[i]);
            _ = consensusService.Consensus.PreVoteAsync(preVote, default);
        }

        foreach (var i in new int[] { 1, 2, 3 })
        {
            var preCommit = new VoteBuilder
            {
                Validator = Validators[i],
                Height = 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Type = VoteType.PreCommit,
            }.Create(PrivateKeys[i]);
            _ = consensusService.Consensus.PreCommitAsync(preCommit, default);
        }

        Assert.Equal(1, consensusService.Height);
        var watch = Stopwatch.StartNew();
        // Assert.True(onTipChanged.WaitOne(5000), "Tip was not changed in time.");
        await tipChangedTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(watch.ElapsedMilliseconds < (actionDelay * 0.5));
        watch.Restart();

        // Assert.True(enteredHeightTwo.WaitOne(5000), "Consensus did not enter height 2 in time.");
        await heightChangedTask.WaitAsync(TimeSpan.FromSeconds(5));
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
        // using var preVoteStepEvent = new ManualResetEvent(false);
        // using var preCommitStepEvent = new ManualResetEvent(false);
        // Proposal? proposedBlock = null;
        int numPreVotes = 0;
        var options = new ConsensusOptions
        {
            EnterPreCommitDelay = delay,
        };
        var blockchain = MakeBlockchain();
        await using var consensus = CreateConsensus(options: options);
        var controller = CreateConsensusController(consensus, blockchain: blockchain);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreVote);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreCommit);
        var proposedTask = controller.ShouldPropose.WaitAsync();
        // var preVotedTask = consensus.PreVoted.WaitAsync();

        // using var _1 = consensus.StepChanged.Subscribe(step =>
        // {
        //     if (step == ConsensusStep.PreVote)
        //     {
        //         preVoteStepEvent.Set();
        //     }
        //     else if (step == ConsensusStep.PreCommit)
        //     {
        //         preCommitStepEvent.Set();
        //     }
        // });
        // using var _2 = consensus.ShouldPropose.Subscribe(proposal =>
        // {
        //     proposedBlock = proposal.Block;
        // });
        using var _3 = consensus.PreVoted.Subscribe(vote =>
        {
            if (vote.Type == VoteType.PreVote)
            {
                numPreVotes++;
            }
        });
        await consensus.StartAsync(default);
        await preVoteStepTask.WaitAsync(TimeSpan.FromSeconds(1));
        var proposal = await proposedTask.WaitAsync(TimeSpan.FromSeconds(1));
        var block = proposal.Block;
        // Assert.True(preVoteStepEvent.WaitOne(1000), "Consensus did not enter PreVote step in time.");
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        // if (proposal.Block is not { } block)
        // {
        //     throw new XunitException("No proposal is made");
        // }

        for (var i = 0; i < 3; i++)
        {
            var preVote = new VoteMetadata
            {
                Height = block.Height,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = PrivateKeys[i].Address,
                ValidatorPower = Validators[i].Power,
                Type = VoteType.PreVote,
            }.Sign(PrivateKeys[i]);
            _ = consensus.PreVoteAsync(preVote, default);
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
                    Validator = PrivateKeys[3].Address,
                    ValidatorPower = Validators[3].Power,
                    Type = VoteType.PreVote,
                }.Sign(PrivateKeys[3]);
                _ = consensus.PreVoteAsync(preVote, default);
            },
            cancellationToken.Token);

        // Assert.True(preCommitStepEvent.WaitOne(1000), "Consensus did not enter PreCommit step in time.");
        await preCommitStepTask.WaitAsync(TimeSpan.FromSeconds(1));
        await cancellationToken.CancelAsync();

        Assert.Equal(delay < preVoteDelay ? 3 : 4, numPreVotes);
    }
}
