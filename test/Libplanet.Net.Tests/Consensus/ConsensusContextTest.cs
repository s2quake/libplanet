using System.Reactive.Linq;
using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Consensus.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Libplanet.Types.Threading;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ConsensusContextTest(ITestOutputHelper output)
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task NewHeightIncreasing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[2]);
        await using var transportB = CreateTransport(Signers[3]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[3], blockchain, transportB, options);

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusService.StartAsync();

        var proposeStepChangedTask3 = consensusService.StepChanged.WaitAsync(
            e => consensusService.Height == 3 && e == ConsensusStep.Propose);
        var endCommitStepChangedTask3 = consensusService.StepChanged.WaitAsync(
            e => consensusService.Height == 3 && e == ConsensusStep.EndCommit);
        var proposeStepChangedTask4 = consensusService.StepChanged.WaitAsync(
            e => consensusService.Height == 4 && e == ConsensusStep.Propose);
        var proposedTask3 = consensusService.BlockProposed.WaitAsync(
            e => e.Height == 3);
        var tipChangedTask3 = blockchain.TipChanged.WaitAsync(e => e.Height == 3L);

        blockchain.ProposeAndAppend(Signers[1]);
        blockchain.ProposeAndAppend(Signers[2]);
        Assert.Equal(2, blockchain.Tip.Height);

        // Wait for context of height 3 to start.
        await proposeStepChangedTask3.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(3, consensusService.Height);

        var proposal3 = await proposedTask3.WaitAsync(WaitTimeout5, cancellationToken);
        var preCommit = new VoteBuilder
        {
            Validator = Validators[0],
            Block = proposal3.Block,
            Type = VoteType.PreCommit,
        }.Create(Signers[0]);
        var preCommitMessage0 = new ConsensusPreCommitMessage
        {
            PreCommit = preCommit,
        };
        transportA.Post(transportB.Peer, preCommitMessage0);

        var preCommit1 = new VoteBuilder
        {
            Validator = Validators[1],
            Block = proposal3.Block,
            Type = VoteType.PreCommit,
        }.Create(Signers[1]);
        var preCommitMessage1 = new ConsensusPreCommitMessage
        {
            PreCommit = preCommit1,
        };
        transportA.Post(transportB.Peer, preCommitMessage1);

        var preCommit2 = new VoteBuilder
        {
            Validator = Validators[2],
            Block = proposal3.Block,
            Type = VoteType.PreCommit,
        }.Create(Signers[2]);
        var preCommitMessage2 = new ConsensusPreCommitMessage
        {
            PreCommit = preCommit2,
        };
        transportA.Post(transportB.Peer, preCommitMessage2);

        // Waiting for commit.
        await endCommitStepChangedTask3.WaitAsync(WaitTimeout5, cancellationToken);
        await tipChangedTask3.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(3, blockchain.Tip.Height);

        // Next height starts normally.
        await proposeStepChangedTask4.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(4, consensusService.Height);
        Assert.Equal(0, consensusService.Round);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Ctor()
    {
        var blockchain = MakeBlockchain();
        await using var transport = CreateTransport(Signers[1]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[1], blockchain, transport, options);

        Assert.Equal(ConsensusStep.Default, consensusService.Step);
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(-1, consensusService.Round);
    }

    [Fact]
    public async Task CannotStartTwice()
    {
        var blockchain = MakeBlockchain();
        await using var transport = CreateTransport(Signers[1]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[1], blockchain, transport, options);
        await transport.StartAsync();
        await consensusService.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => consensusService.StartAsync());
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task NewHeightWhenTipChanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var newHeightDelay = TimeSpan.FromSeconds(1);
        var blockchain = MakeBlockchain();
        await using var transport = CreateTransport(Signers[1]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[1], blockchain, transport, options);

        await transport.StartAsync();
        await consensusService.StartAsync();

        Assert.Equal(1, consensusService.Height);

        blockchain.ProposeAndAppend(RandomUtility.Signer(random));
        Assert.Equal(1, consensusService.Height);
        await Task.Delay(newHeightDelay + TimeSpan.FromSeconds(1), cancellationToken);
        Assert.Equal(2, consensusService.Height);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreMessagesFromLowerHeight()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[1]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[1], blockchain, transportB, options);
        var heightChangedTask2 = consensusService.HeightChanged.WaitAsync(e => e == 2);
        var messageHandlingFailedTask = transportB.MessageRouter.MessageHandlingFailed.WaitAsync();

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusService.StartAsync();

        Assert.Equal(1, consensusService.Height);

        blockchain.ProposeAndAppend(Signers[0]);
        await heightChangedTask2.WaitAsync(WaitTimeout5, cancellationToken);

        var proposal = new ProposalBuilder
        {
            Block = blockchain.Blocks[1],
        }.Create(Signers[0]);
        var proposalMessage = new ConsensusProposalMessage
        {
            Proposal = proposal,
        };
        transportA.Post(transportB.Peer, proposalMessage);
        var (h, e) = await messageHandlingFailedTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.IsType<ConsensusProposalMessageHandler>(h);
        Assert.IsType<InvalidMessageException>(e);
        Assert.StartsWith("Proposal height is lower", e.Message);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task VoteSetGetOnlyProposeCommitHash()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var preCommitList = new List<Vote>();
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[1]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[1], blockchain, transportB, options);
        var proposedTask1 = consensusService.BlockProposed.WaitAsync(_ => consensusService.Height == 1);
        var endCommitStepTask1 = consensusService.StepChanged.WaitAsync(
            e => consensusService.Height == 1 && e == ConsensusStep.EndCommit);

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusService.StartAsync(cancellationToken);

        var proposal = await proposedTask1.WaitAsync(WaitTimeout5, cancellationToken);
        preCommitList.Add(new VoteMetadata
        {
            Validator = Validators[0].Address,
            ValidatorPower = Validators[0].Power,
            Height = 1,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Type = VoteType.PreCommit,
        }.Sign(Signers[0]));
        preCommitList.AddRange(Enumerable.Range(1, 3).Select(i => new VoteBuilder
        {
            Validator = Validators[i],
            Block = proposal.Block,
            Type = VoteType.PreCommit,
        }.Create(Signers[i])));

        foreach (var preCommit in preCommitList)
        {
            transportA.Post(transportB.Peer, new ConsensusPreCommitMessage { PreCommit = preCommit });
        }

        await endCommitStepTask1.WaitAsync(WaitTimeout5, cancellationToken);

        var blockCommit = consensusService.Consensus.Round.PreCommits.GetBlockCommit();
        Assert.NotEqual(preCommitList[0], blockCommit.Votes.First(i => i.Validator == Signers[0].Address));

        var actualVotesWithoutInvalid = blockCommit.Votes.Where(i => i.Validator != Signers[0].Address).ToHashSet();
        var expectedVotes = preCommitList.Where(i => i.Validator != Signers[0].Address).ToHashSet();

        Assert.Equal(expectedVotes, actualVotesWithoutInvalid);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task GetVoteSetBits()
    {
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[3]);
        await using var transportB = CreateTransport(Signers[0]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[0], blockchain, transportB, options);
        var preCommitStepChangedTask = consensusService.StepChanged.WaitAsync(
            e => consensusService.Height == 1 && e == ConsensusStep.PreCommit);
        var preCommittedTask = consensusService.Consensus.PreCommitted.WaitAsync(
            e => e.Height == 1);

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusService.StartAsync();

        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);
        var preVote1 = new VoteBuilder
        {
            Validator = Validators[1],
            Block = block,
            Type = VoteType.PreVote,
        }.Create(Signers[1]);
        var preVote3 = new VoteBuilder
        {
            Validator = Validators[3],
            Block = block,
            Type = VoteType.PreVote,
        }.Create(Signers[3]);

        transportA.Post(transportB.Peer, new ConsensusProposalMessage { Proposal = proposal });
        transportA.Post(transportB.Peer, new ConsensusPreVoteMessage { PreVote = preVote1 });
        transportA.Post(transportB.Peer, new ConsensusPreVoteMessage { PreVote = preVote3 });

        await TaskUtility.WhenAll(
            WaitTimeout5,
            preCommitStepChangedTask,
            preCommittedTask);

        // VoteSetBits expects missing votes
        var bits1 = consensusService.Consensus.Round.PreVotes.GetBits(block.BlockHash);
        Assert.Equal([true, true, false, true], bits1.ToArray());
        var bits2 = consensusService.Consensus.Round.PreCommits.GetBits(block.BlockHash);
        Assert.Equal([true, false, false, false], bits2.ToArray());
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task HandleVoteSetBits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[3]);
        await using var transportB = CreateTransport(Signers[0]);
        var options = new ConsensusServiceOptions
        {
            Validators = [transportA.Peer],
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[0], blockchain, transportB, options);
        var preCommitStepChangedTask = consensusService.StepChanged.WaitAsync(
            e => consensusService.Height == 1 && e == ConsensusStep.PreCommit);

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusService.StartAsync();

        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);
        var preVote1 = new VoteBuilder
        {
            Validator = Validators[1],
            Block = block,
            Type = VoteType.PreVote,
        }.Create(Signers[1]);
        var preVote2 = new VoteBuilder
        {
            Validator = Validators[2],
            Block = block,
            Type = VoteType.PreVote,
        }.Create(Signers[2]);
        transportA.Post(transportB.Peer, new ConsensusProposalMessage { Proposal = proposal });
        transportA.Post(transportB.Peer, new ConsensusPreVoteMessage { PreVote = preVote1 });
        transportA.Post(transportB.Peer, new ConsensusPreVoteMessage { PreVote = preVote2 });

        await preCommitStepChangedTask.WaitAsync(WaitTimeout5, cancellationToken);

        // VoteSetBits expects missing votes
        var voteBits = new VoteBitsBuilder
        {
            Validator = Validators[3],
            Block = block,
            Bits = [false, false, true, false],
            VoteType = VoteType.PreVote,
        }.Create(Signers[3]);
        var voteBitsMessage = new ConsensusVoteBitsMessage
        {
            VoteBits = voteBits,
        };
        var messageList = new List<ConsensusPreVoteMessage>();
        try
        {
            await foreach (var item in transportA.SendAsync<ConsensusPreVoteMessage>(
                peer: transportB.Peer,
                message: voteBitsMessage,
                isLast: _ => false,
                cancellationToken: cancellationToken))
            {
                messageList.Add(item);
            }
        }
        catch (TimeoutException)
        {
            // do nothing
        }

        var votes = messageList.OrderBy(item => item.PreVote.Validator).Select(item => item.PreVote).ToArray();
        Assert.True(votes.All(vote => vote.Type == VoteType.PreVote));
        Assert.Equal(2, votes.Length);
        Assert.Equal(Signers[0].Address, votes[0].Validator);
        Assert.Equal(Signers[1].Address, votes[1].Validator);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task HandleProposalClaim()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[1]);
        await using var transportB = CreateTransport(Signers[0]);
        var options = new ConsensusServiceOptions
        {
            Validators = [transportA.Peer],
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusServiceB = new ConsensusService(Signers[0], blockchain, transportB, options);
        var preVoteStepTask = consensusServiceB.StepChanged.WaitAsync(
            e => consensusServiceB.Height == 1 && e == ConsensusStep.PreVote);

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusServiceB.StartAsync();

        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);
        transportA.Post(transportB.Peer, new ConsensusProposalMessage { Proposal = proposal });
        await preVoteStepTask.WaitAsync(WaitTimeout5, cancellationToken);

        // ProposalClaim expects corresponding proposal if exists
        var proposalClaim = new ProposalClaimBuilder
        {
            Validator = Validators[1],
            Block = block,
        }.Create(Signers[1]);

        var reply = await transportA.SendAsync<ConsensusProposalMessage>(
            peer: transportB.Peer,
            message: new ConsensusProposalClaimMessage { ProposalClaim = proposalClaim },
            cancellationToken: cancellationToken);
        Assert.Equal(proposal, reply.Proposal);
    }
}
