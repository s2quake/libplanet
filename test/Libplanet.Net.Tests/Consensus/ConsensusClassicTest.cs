using System.Diagnostics;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Types;
using Libplanet.Extensions;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using System.Reactive.Linq;

using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ConsensusClassicTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task StartAsProposer()
    {
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        using var observer = new ConsensusObserver(Signers[1], consensus, blockchain);

        consensus.StartAfter(100);

        var proposal = await observer.ShouldPropose.WaitAsync(WaitTimeout5);
        _ = consensus.ProposeAsync(proposal);
        await observer.ShouldPreVote.WaitAsync(WaitTimeout5);

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
    }

    [Fact(Timeout = Timeout)]
    public async Task StartAsProposerWithLastCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators, height: 2);
        using var observer = new ConsensusObserver(Signers[2], consensus, blockchain);
        var (_, blockCommit1) = blockchain.ProposeAndAppend(Signers[1]);

        consensus.StartAfter(100);
        var proposeTask = observer.ShouldPropose.WaitAsync();
        var proposal = await proposeTask.WaitAsync(WaitTimeout5, cancellationToken);
        _ = consensus.ProposeAsync(proposal, cancellationToken);
        var preVoteTask = observer.ShouldPreVote.WaitAsync();
        var preVote = await preVoteTask.WaitAsync(WaitTimeout5, cancellationToken);
        _ = consensus.PreVoteAsync(preVote, cancellationToken);

        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(blockCommit1, proposal.Block.PreviousBlockCommit);
    }

    [Fact(Timeout = Timeout)]
    public async Task CannotStartTwice()
    {
        await using var consensus = new Net.Consensus.Consensus(Validators);

        await consensus.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(consensus.StartAsync);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanAcceptMessagesAfterCommitFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        blockchain.ProposeAndAppend(Signers[1]);

        await using var consensus = new Net.Consensus.Consensus(Validators, height: 2);
        var observer = new ConsensusObserver(Signers[2], consensus, blockchain);

        consensus.StartAfter(100);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreVote);
        var endCommitStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.EndCommit);
        var proposeTask = observer.ShouldPropose.WaitAsync();
        var proposal = await proposeTask.WaitAsync(WaitTimeout5, cancellationToken);

        _ = consensus.ProposeAsync(proposal, cancellationToken);
        await preVoteStepTask.WaitAsync(WaitTimeout5, cancellationToken);

        blockchain.AppendWithBlockCommit(proposal.Block);

        Assert.Equal(2, blockchain.Tip.Height);

        // Make PreVotes to normally move to PreCommit step.
        foreach (var i in new int[] { 0, 1, 2, 3 })
        {
            var vote = new VoteBuilder
            {
                Validator = Validators[i],
                Block = proposal.Block,
                Type = VoteType.PreVote,
            }.Create(Signers[i]);
            _ = consensus.PreVoteAsync(vote, cancellationToken);
        }

        // Validator 2 will automatically vote its PreCommit.
        foreach (var i in new int[] { 0, 1, 2 })
        {
            var vote = new VoteBuilder
            {
                Validator = Validators[i],
                Block = proposal.Block,
                Type = VoteType.PreCommit,
            }.Create(Signers[i]);
            _ = consensus.PreCommitAsync(vote, cancellationToken);
        }

        await endCommitStepTask.WaitAsync(WaitTimeout5, cancellationToken);

        // Check consensus has only three votes.
        var preCommits = consensus.Round.PreCommits;
        var blockCommit = preCommits.GetBlockCommit();
        Assert.Equal(3, blockCommit.Votes.Where(vote => vote.Type == VoteType.PreCommit).Count());

        // Context should still accept new votes.
        var vote3 = new VoteBuilder
        {
            Validator = Validators[3],
            Block = proposal.Block,
            Type = VoteType.PreCommit,
        }.Create(Signers[3]);

        await consensus.PreCommitAsync(vote3).WaitAsync(WaitTimeout5, cancellationToken);

        blockCommit = preCommits.GetBlockCommit();
        Assert.Equal(4, blockCommit.Votes.Where(vote => vote.Type == VoteType.PreCommit).Count());
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowOnInvalidProposerMessage()
    {
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var block = blockchain.Propose(Signers[0]);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 0,
        }.Create(Signers[0]);

        await consensus.StartAsync();

        var e = await Assert.ThrowsAsync<ArgumentException>(() => consensus.ProposeAsync(proposal));
        Assert.Equal("proposal", e.ParamName);
        Assert.StartsWith("Given proposal's proposer", e.Message);
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowOnDifferentHeightMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        using var observer = new ConsensusObserver(Signers[1], consensus, blockchain);
        var proposeTask = observer.ShouldPropose.WaitAsync();
        var signer = Signers[2];
        var invalidBlock = new BlockBuilder
        {
            Height = 2,
        }.Create(signer);
        var invalidProposal = new ProposalBuilder
        {
            Block = invalidBlock,
            Round = 2,
        }.Create(signer);
        var invalidVote = new VoteBuilder
        {
            Validator = Validators[2],
            Block = invalidBlock,
            Type = VoteType.PreVote,
        }.Create(signer);

        await consensus.StartAsync();
        var e1 = await Assert.ThrowsAsync<ArgumentException>(() => consensus.ProposeAsync(invalidProposal));
        Assert.StartsWith("Height of proposal", e1.Message);

        var proposal = await proposeTask.WaitAsync(WaitTimeout5, cancellationToken);
        await consensus.ProposeAsync(proposal).WaitAsync(WaitTimeout5, cancellationToken);

        var e2 = await Assert.ThrowsAsync<ArgumentException>(() => consensus.PreVoteAsync(invalidVote));
        Assert.StartsWith("Height of vote", e2.Message);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanPreCommitOnEndCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchainOptions = new BlockchainOptions
        {
            SystemAction = new SystemAction
            {
                LeaveBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MaxActionBytes = 50 * 1024,
            },
        };
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock, blockchainOptions);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        using var observer = new ConsensusObserver(Signers[0], consensus, blockchain);
        using var _1 = consensus.Finalized.Subscribe(e => blockchain.Append(e.Block, e.BlockCommit));

        var preVoteStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreVote);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreCommit);
        var endCommitStepTask = consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.EndCommit);

        var tipChangedTask = blockchain.TipChanged.WaitAsync(e => e.Height == 1);

        var action = new DelayAction(100);
        _ = blockchain.StagedTransactions.Add(Signers[1], new()
        {
            Actions = [action],
        });
        var block = blockchain.Propose(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = 0,
        }.Create(Signers[1]);

        await consensus.StartAsync(cancellationToken);
        var preCommits = consensus.Round.PreCommits;
        _ = consensus.ProposeAsync(proposal, cancellationToken);

        foreach (var i in new int[] { 0, 1, 2, 3 })
        {
            var preVote = new VoteBuilder
            {
                Validator = Validators[i],
                Block = block,
                Type = VoteType.PreVote,
            }.Create(Signers[i]);
            _ = consensus.PreVoteAsync(preVote, cancellationToken);
        }

        // Two additional votes should be enough to reach a consensus.
        foreach (var i in new int[] { 0, 1, 2 })
        {
            var preCommit = new VoteBuilder
            {
                Validator = Validators[i],
                Block = block,
                Type = VoteType.PreCommit,
            }.Create(Signers[i]);
            _ = consensus.PreCommitAsync(preCommit, cancellationToken);
        }

        await tipChangedTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(
            3,
            preCommits.GetBlockCommit().Votes.Count(vote => vote.Type == VoteType.PreCommit));

        await preVoteStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        await preCommitStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        await endCommitStepTask.WaitAsync(WaitTimeout5, cancellationToken);

        // Add the last vote and wait for it to be consumed.
        var vote = new VoteBuilder
        {
            Validator = Validators[3],
            Block = block,
            Timestamp = DateTimeOffset.UtcNow,
            Type = VoteType.PreCommit,
        }.Create(Signers[3]);
        await consensus.PreCommitAsync(vote, cancellationToken);
        Assert.Equal(4, preCommits.GetBlockCommit().Votes.Count(vote => vote.Type == VoteType.PreCommit));
    }

    [Fact(Timeout = Timeout)]
    public async Task CanReplaceProposal()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        using var observer = new ConsensusObserver(Signers[0], consensus, blockchain);
        var blockA = blockchain.Propose(Signers[1]);
        var blockB = blockchain.Propose(Signers[1]);
        await consensus.StartAsync(cancellationToken);
        Assert.Equal(ConsensusStep.Propose, consensus.Step);

        var proposalA = new ProposalBuilder
        {
            Block = blockA,
        }.Create(Signers[1]);
        var preVoteA2 = new VoteBuilder
        {
            Validator = Validators[3],
            Block = blockA,
            Type = VoteType.PreVote,
        }.Create(Signers[3]);
        var proposalB = new ProposalBuilder
        {
            Block = blockB,
        }.Create(Signers[1]);
        await consensus.ProposeAsync(proposalA).WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(proposalA, consensus.Proposal);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);

        // Proposal B is ignored because proposal A is received first.
        var e1 = await Assert.ThrowsAsync<InvalidOperationException>(() => consensus.ProposeAsync(proposalB));
        Assert.StartsWith("Proposal already exists", e1.Message);

        Assert.Equal(proposalA, consensus.Proposal);
        _ = consensus.PreVoteAsync(preVoteA2, cancellationToken);

        // Validator 1 (key1) collected +2/3 pre-vote messages,
        // sends maj23 message to consensus.
        var maj23 = new Maj23Builder
        {
            Validator = Validators[2],
            Block = blockB,
            VoteType = VoteType.PreVote,
        }.Create(Signers[2]);
        consensus.AddPreVoteMaj23(maj23);

        var preVoteB0 = new VoteBuilder
        {
            Validator = Validators[1],
            Block = blockB,
            Type = VoteType.PreVote,
        }.Create(Signers[1]);
        var preVoteB1 = new VoteBuilder
        {
            Validator = Validators[2],
            Block = blockB,
            Type = VoteType.PreVote,
        }.Create(Signers[2]);
        var preVoteB2 = new VoteBuilder
        {
            Validator = Validators[3],
            Block = blockB,
            Type = VoteType.PreVote,
        }.Create(Signers[3]);
        _ = consensus.PreVoteAsync(preVoteB0, cancellationToken);
        _ = consensus.PreVoteAsync(preVoteB1, cancellationToken);
        _ = consensus.PreVoteAsync(preVoteB2, cancellationToken);
        await consensus.ProposalClaimed.WaitAsync(WaitTimeout5);
        Assert.Null(consensus.Proposal);
        _ = consensus.ProposeAsync(proposalB);
        await consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreCommit, WaitTimeout5);
        Assert.Equal(consensus.Proposal, proposalB);
        Assert.Equal(proposalB, consensus.ValidProposal);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanCreateContextWithLastingEvaluation()
    {
        const int actionDelay = 2000;
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transport = new NetMQ.NetMQTransport(Signers[0]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromMilliseconds(100),
        };
        var consensusService = new ConsensusService(Signers[0], blockchain, transport, options);
        var tipChangedTask = blockchain.TipChanged.WaitAsync(e => e.Height == 1);
        var heightChangedTask = consensusService.HeightChanged.WaitAsync(e => e == 2);

        _ = blockchain.StagedTransactions.Add(Signers[1], new()
        {
            Actions = [new DelayAction(actionDelay)],
        });
        var block = blockchain.Propose(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);

        await transport.StartAsync();
        await consensusService.StartAsync();
        var consensus = consensusService.Consensus;
        var preCommits = consensus.Round.PreCommits;
        _ = consensus.ProposeAsync(proposal);

        foreach (var i in new int[] { 1, 2, 3 })
        {
            var preVote = new VoteBuilder
            {
                Validator = Validators[i],
                Block = block,
                Type = VoteType.PreVote,
            }.Create(Signers[i]);
            _ = consensus.PreVoteAsync(preVote);
        }

        foreach (var i in new int[] { 1, 2, 3 })
        {
            var preCommit = new VoteBuilder
            {
                Validator = Validators[i],
                Block = block,
                Type = VoteType.PreCommit,
            }.Create(Signers[i]);
            _ = consensus.PreCommitAsync(preCommit);
        }

        Assert.Equal(1, consensusService.Height);
        var stopWatch = Stopwatch.StartNew();
        await tipChangedTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.True(stopWatch.ElapsedMilliseconds < (actionDelay * 0.5));
        stopWatch.Restart();

        await heightChangedTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(
            4,
            preCommits.GetBlockCommit().Votes.Count(vote => vote.Type == VoteType.PreCommit));
        Assert.True(stopWatch.ElapsedMilliseconds > (actionDelay * 0.5));
        Assert.Equal(2, consensusService.Height);
    }

    [Theory(Timeout = Timeout)]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task CanCollectPreVoteAfterMajority(int delay)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new ConsensusOptions
        {
            EnterPreCommitDelay = delay,
        };
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators, height: 1, options);
        var block = blockchain.Propose(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);
        using var preVoteCounter = consensus.PreVoted.Counter();

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(proposal, cancellationToken);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);

        for (var i = 0; i < 3; i++)
        {
            var preVote = new VoteBuilder
            {
                Validator = Validators[i],
                Block = block,
                Type = VoteType.PreVote,
            }.Create(Signers[i]);
            _ = consensus.PreVoteAsync(preVote, cancellationToken);
        }

        // Send delayed PreVote message after sending preCommit message
        using var cancellationTokenSource = new CancellationTokenSource();
        const int preVoteDelay = 300;
        _ = Task.Run(
            async () =>
            {
                await Task.Delay(preVoteDelay, cancellationTokenSource.Token);
                var preVote = new VoteBuilder
                {
                    Validator = Validators[3],
                    Block = block,
                    Type = VoteType.PreVote,
                }.Create(Signers[3]);
                _ = consensus.PreVoteAsync(preVote);
            },
            cancellationTokenSource.Token);

        await consensus.StepChanged.WaitAsync(e => e.Step == ConsensusStep.PreCommit, WaitTimeout5);
        await cancellationTokenSource.CancelAsync();

        Assert.Equal(delay < preVoteDelay ? 3 : 4, preVoteCounter.Count);
    }
}
