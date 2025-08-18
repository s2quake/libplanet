using System.Reactive.Linq;
using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Consensus.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Nito.AsyncEx;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextTest
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task NewHeightIncreasing()
    {
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
        var tipChangedTask3 = blockchain.TipChanged.WaitAsync(e => e.Tip.Height == 3L);

        blockchain.ProposeAndAppend(Signers[1]);
        blockchain.ProposeAndAppend(Signers[2]);
        Assert.Equal(2, blockchain.Tip.Height);

        // Wait for context of height 3 to start.
        await proposeStepChangedTask3.WaitAsync(WaitTimeout);
        Assert.Equal(3, consensusService.Height);

        var proposal3 = await proposedTask3.WaitAsync(WaitTimeout);
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
        await endCommitStepChangedTask3.WaitAsync(WaitTimeout);
        await tipChangedTask3.WaitAsync(WaitTimeout);
        Assert.Equal(3, blockchain.Tip.Height);

        // Next height starts normally.
        await proposeStepChangedTask4.WaitAsync(WaitTimeout);
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

        blockchain.ProposeAndAppend(new PrivateKey());
        Assert.Equal(1, consensusService.Height);
        await Task.Delay(newHeightDelay + TimeSpan.FromSeconds(1));
        Assert.Equal(2, consensusService.Height);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreMessagesFromLowerHeight()
    {
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
        await heightChangedTask2.WaitAsync(WaitTimeout);

        var proposal = new ProposalBuilder
        {
            Block = blockchain.Blocks[1],
        }.Create(Signers[0]);
        var proposalMessage = new ConsensusProposalMessage
        {
            Proposal = proposal,
        };
        transportA.Post(transportB.Peer, proposalMessage);
        var (h, e) = await messageHandlingFailedTask.WaitAsync(WaitTimeout);
        Assert.IsType<ConsensusProposalMessageHandler>(h);
        Assert.IsType<InvalidMessageException>(e);
        Assert.StartsWith("Proposal height is lower", e.Message);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task VoteSetGetOnlyProposeCommitHash()
    {
        ConsensusProposalMessage? proposal = null;
        var heightOneProposalSent = new AsyncAutoResetEvent();
        var heightOneEndCommit = new AsyncAutoResetEvent();
        var votes = new List<Vote>();

        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: PrivateKeys[1]);
        // consensusService.StateChanged += (sender, tuple) =>
        // {
        //     if (tuple.Height == 1 && tuple.Step == ConsensusStep.EndCommit)
        //     {
        //         heightOneEndCommit.Set();
        //     }
        // };
        // consensusService.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 1 && eventArgs.Message is ConsensusProposalMessage proposalMsg)
        //     {
        //         proposal = proposalMsg;
        //         heightOneProposalSent.Set();
        //     }
        // };

        await consensusService.StartAsync(default);
        await heightOneProposalSent.WaitAsync();
        BlockHash proposedblockHash = Assert.IsType<BlockHash>(proposal?.BlockHash);

        votes.Add(new VoteMetadata
        {
            Validator = Validators[0].Address,
            ValidatorPower = Validators[0].Power,
            Height = 1,
            BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            Type = VoteType.PreCommit,
        }.Sign(PrivateKeys[0]));
        votes.AddRange(Enumerable.Range(1, 3).Select(x => new VoteMetadata
        {
            Validator = Validators[x].Address,
            ValidatorPower = Validators[x].Power,
            Height = 1,
            BlockHash = proposedblockHash,
            Type = VoteType.PreCommit,
        }.Sign(PrivateKeys[x])));

        foreach (var vote in votes)
        {
            // await consensusService.HandleMessageAsync(new ConsensusPreCommitMessage { PreCommit = vote }, default);
            transportA.Post(transportB.Peer, new ConsensusPreCommitMessage { PreCommit = vote });
        }

        await heightOneEndCommit.WaitAsync();

        var blockCommit = consensusService.Consensus.Round.PreCommits.GetBlockCommit();
        Assert.NotNull(blockCommit);
        Assert.NotEqual(votes[0], blockCommit!.Votes.First(x =>
            x.Validator.Equals(PrivateKeys[0].PublicKey)));

        var actualVotesWithoutInvalid
            = blockCommit.Votes.Where(x => !x.Validator.Equals(PrivateKeys[0].PublicKey)).ToHashSet();

        var expectedVotes
            = votes.Where(x => !x.Validator.Equals(PrivateKeys[0].PublicKey)).ToHashSet();

        Assert.Equal(expectedVotes, actualVotesWithoutInvalid);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task GetVoteSetBits()
    {
        PrivateKey proposer = PrivateKeys[1];
        BigInteger proposerPower = Validators[1].Power;
        AsyncAutoResetEvent stepChanged = new AsyncAutoResetEvent();
        AsyncAutoResetEvent committed = new AsyncAutoResetEvent();
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: PrivateKeys[0]);
        await consensusService.StartAsync(default);
        var block = blockchain.ProposeBlock(proposer);
        var proposal = new ProposalMetadata
        {
            BlockHash = block.BlockHash,
            Height = 1,
            Round = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            ValidRound = -1,
        }.Sign(proposer, block);
        var preVote1 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = proposer.Address,
            ValidatorPower = proposerPower,
            Type = VoteType.PreVote,
        }.Sign(proposer);
        var preVote2 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = PrivateKeys[2].Address,
            ValidatorPower = Validators[2].Power,
            Type = VoteType.PreVote,
        }.Sign(PrivateKeys[2]);
        var preVote3 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = PrivateKeys[3].Address,
            ValidatorPower = Validators[3].Power,
            Type = VoteType.PreVote,
        }.Sign(PrivateKeys[3]);
        // consensusService.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs is { Height: 1, Step: ConsensusStep.PreCommit })
        //     {
        //         stepChanged.Set();
        //     }
        // };
        // consensusService.CurrentContext.VoteSetModified += (_, eventArgs) =>
        // {
        //     if (eventArgs.Flag == VoteType.PreCommit)
        //     {
        //         committed.Set();
        //     }
        // };

        transportA.Post(transportB.Peer, new ConsensusProposalMessage { Proposal = proposal });
        transportA.Post(transportB.Peer, new ConsensusPreVoteMessage { PreVote = preVote1 });
        transportA.Post(transportB.Peer, new ConsensusPreVoteMessage { PreVote = preVote3 });
        await stepChanged.WaitAsync();
        await committed.WaitAsync();

        // VoteSetBits expects missing votes
        var bits = consensusService.Consensus.Round.PreVotes.GetBits(block.BlockHash);
        Assert.True(bits.SequenceEqual(new[] { true, true, false, true }));
        bits = consensusService.Consensus.Round.PreCommits.GetBits(block.BlockHash);
        Assert.True(
        bits.SequenceEqual(new[] { true, false, false, false }));
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task HandleVoteSetBits()
    {
        PrivateKey proposer = PrivateKeys[1];
        BigInteger proposerPower = Validators[1].Power;
        ConsensusStep step = ConsensusStep.Default;
        var stepChanged = new AsyncAutoResetEvent();
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: PrivateKeys[0]);
        // consensusService.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.Step != step)
        //     {
        //         step = eventArgs.Step;
        //         stepChanged.Set();
        //     }
        // };

        await consensusService.StartAsync(default);
        var block = blockchain.ProposeBlock(proposer);
        var proposal = new ProposalMetadata
        {
            BlockHash = block.BlockHash,
            Height = 1,
            Round = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            ValidRound = -1,
        }.Sign(proposer, block);
        var preVote1 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = proposer.Address,
            ValidatorPower = proposerPower,
            Type = VoteType.PreVote,
        }.Sign(proposer);
        var preVote2 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = PrivateKeys[2].Address,
            ValidatorPower = Validators[2].Power,
            Type = VoteType.PreVote,
        }.Sign(PrivateKeys[2]);
        transportA.Post(transportB.Peer, new ConsensusProposalMessage { Proposal = proposal });
        transportA.Post(transportB.Peer, new ConsensusPreVoteMessage { PreVote = preVote1 });
        transportA.Post(transportB.Peer, new ConsensusPreVoteMessage { PreVote = preVote2 });
        do
        {
            await stepChanged.WaitAsync();
        }
        while (step != ConsensusStep.PreCommit);

        // VoteSetBits expects missing votes
        var voteSetBits =
            new VoteBitsMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = PrivateKeys[1].Address,
                VoteType = VoteType.PreVote,
                Bits = [false, false, true, false],
            }.Sign(PrivateKeys[1]);
        //     ConsensusMessage[] votes =
        // consensusService.HandleVoteSetBits(voteSetBits).ToArray();
        var votes = consensusService.Consensus.Round.PreVotes.GetVotes(voteSetBits.Bits);
        Assert.True(votes.All(vote => vote.Type == VoteType.PreVote));
        Assert.Equal(2, votes.Length);
        Assert.Equal(PrivateKeys[0].Address, votes[0].Validator);
        Assert.Equal(PrivateKeys[1].Address, votes[1].Validator);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task HandleProposalClaim()
    {
        PrivateKey proposer = PrivateKeys[1];
        ConsensusStep step = ConsensusStep.Default;
        var stepChanged = new AsyncAutoResetEvent();
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: PrivateKeys[0]);
        // consensusService.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.Step != step)
        //     {
        //         step = eventArgs.Step;
        //         stepChanged.Set();
        //     }
        // };
        await consensusService.StartAsync(default);
        var block = blockchain.ProposeBlock(proposer);
        var proposal = new ProposalMetadata
        {
            BlockHash = block.BlockHash,
            Height = 1,
            Round = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            ValidRound = -1,
        }.Sign(proposer, block);
        transportA.Post(transportB.Peer, new ConsensusProposalMessage { Proposal = proposal });
        // await consensusService.HandleMessageAsync(new ConsensusProposalMessage { Proposal = proposal }, default);
        await stepChanged.WaitAsync();

        // ProposalClaim expects corresponding proposal if exists
        var proposalClaim =
        new ProposalClaimMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = PrivateKeys[1].Address,
        }.Sign(PrivateKeys[1]);
        // Proposal? reply =
        //     consensusService.HandleProposalClaim(proposalClaim);
        // Assert.NotNull(reply);
        // Assert.Equal(proposal, reply);
    }
}
