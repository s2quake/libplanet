using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Libplanet.Types.Tests;
using Nito.AsyncEx;
using Serilog;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextTest
{
    private const int Timeout = 30000;
    private readonly ILogger _logger;

    public ConsensusContextTest(ITestOutputHelper output)
    {
        const string outputTemplate =
            "{Timestamp:HH:mm:ss:ffffffZ} - {Message} {Exception}";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
            .CreateLogger()
            .ForContext<ConsensusContextTest>();

        _logger = Log.ForContext<ConsensusContextTest>();
    }

    [Fact(Timeout = Timeout)]
    public async Task NewHeightIncreasing()
    {
        ConsensusProposalMessage? proposal = null;
        var proposalMessageSent = new AsyncAutoResetEvent();
        var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
            TimeSpan.FromSeconds(1),
            TestUtils.Options,
            TestUtils.PrivateKeys[3]);
        await consensusContext.StartAsync(default);

        AsyncAutoResetEvent heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
        AsyncAutoResetEvent heightThreeStepChangedToEndCommit = new AsyncAutoResetEvent();
        AsyncAutoResetEvent heightFourStepChangedToPropose = new AsyncAutoResetEvent();
        AsyncAutoResetEvent onTipChangedToThree = new AsyncAutoResetEvent();
        consensusContext.StateChanged += (_, eventArgs) =>
        {
            if (eventArgs.Height == 3 && eventArgs.Step == ConsensusStep.Propose)
            {
                heightThreeStepChangedToPropose.Set();
            }
            else if (eventArgs.Height == 3 && eventArgs.Step == ConsensusStep.EndCommit)
            {
                heightThreeStepChangedToEndCommit.Set();
            }
            else if (eventArgs.Height == 4 && eventArgs.Step == ConsensusStep.Propose)
            {
                heightFourStepChangedToPropose.Set();
            }
        };
        consensusContext.MessagePublished += (_, eventArgs) =>
        {
            if (eventArgs.Message is ConsensusProposalMessage proposalMsg)
            {
                proposal = proposalMsg;
                proposalMessageSent.Set();
            }
        };
        using var _ = blockChain.TipChanged.Subscribe(eventArgs =>
        {
            if (eventArgs.Tip.Height == 3L)
            {
                onTipChangedToThree.Set();
            }
        });

        var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        blockChain.Append(block, blockCommit);
        block = blockChain.ProposeBlock(TestUtils.PrivateKeys[2]);
        blockChain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Equal(2, blockChain.Tip.Height);

        // Wait for context of height 3 to start.
        await heightThreeStepChangedToPropose.WaitAsync();
        Assert.Equal(3, consensusContext.Height);

        // Cannot call NewHeight() with invalid heights.
        await Assert.ThrowsAsync<InvalidHeightIncreasingException>(
            async () => await consensusContext.NewHeightAsync(2, default));
        await Assert.ThrowsAsync<InvalidHeightIncreasingException>(
            async () => await consensusContext.NewHeightAsync(3, default));

        await proposalMessageSent.WaitAsync();
        BlockHash proposedblockHash = Assert.IsType<BlockHash>(proposal?.BlockHash);

        consensusContext.HandleMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[0],
                    TestUtils.Validators[0].Power,
                    3,
                    0,
                    hash: proposedblockHash,
                    flag: VoteType.PreCommit)
            });
        consensusContext.HandleMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[1],
                    TestUtils.Validators[1].Power,
                    3,
                    0,
                    hash: proposedblockHash,
                    flag: VoteType.PreCommit)
            });
        consensusContext.HandleMessage(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    3,
                    0,
                    hash: proposedblockHash,
                    flag: VoteType.PreCommit)
            });

        // Waiting for commit.
        await heightThreeStepChangedToEndCommit.WaitAsync();
        await onTipChangedToThree.WaitAsync();
        Assert.Equal(3, blockChain.Tip.Height);

        // Next height starts normally.
        await heightFourStepChangedToPropose.WaitAsync();
        Assert.Equal(4, consensusContext.Height);
        Assert.Equal(0, consensusContext.Round);
    }

    [Fact(Timeout = Timeout)]
    public void Ctor()
    {
        var (_, consensusContext) = TestUtils.CreateDummyConsensusContext(
            TimeSpan.FromSeconds(1),
            TestUtils.Options,
            TestUtils.PrivateKeys[1]);

        Assert.Equal(ConsensusStep.Default, consensusContext.Step);
        Assert.Equal(1, consensusContext.Height);
        Assert.Equal(-1, consensusContext.Round);
    }

    [Fact]
    public async Task CannotStartTwice()
    {
        var (_, consensusContext) = TestUtils.CreateDummyConsensusContext(
            TimeSpan.FromSeconds(1),
            TestUtils.Options,
            TestUtils.PrivateKeys[1]);
        await consensusContext.StartAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(
            async() => await consensusContext.StartAsync(default));
    }

    [Fact(Timeout = Timeout)]
    public async Task NewHeightWhenTipChanged()
    {
        var newHeightDelay = TimeSpan.FromSeconds(1);
        var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
            newHeightDelay,
            TestUtils.Options,
            TestUtils.PrivateKeys[1]);
        await consensusContext.StartAsync(default);

        Assert.Equal(1, consensusContext.Height);
        Block block = blockChain.ProposeBlock(new PrivateKey());
        blockChain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Equal(1, consensusContext.Height);
        await Task.Delay(newHeightDelay + TimeSpan.FromSeconds(1));
        Assert.Equal(2, consensusContext.Height);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreMessagesFromLowerHeight()
    {
        var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
            TimeSpan.FromSeconds(1),
            TestUtils.Options,
            TestUtils.PrivateKeys[1]);
        await consensusContext.StartAsync(default);
        Assert.True(consensusContext.Height == 1);
        Assert.False(consensusContext.HandleMessage(
            TestUtils.CreateConsensusPropose(
                blockChain.ProposeBlock(TestUtils.PrivateKeys[0]),
                TestUtils.PrivateKeys[0],
                0)));
    }

    [Fact(Timeout = Timeout)]
    public async Task VoteSetGetOnlyProposeCommitHash()
    {
        ConsensusProposalMessage? proposal = null;
        var heightOneProposalSent = new AsyncAutoResetEvent();
        var heightOneEndCommit = new AsyncAutoResetEvent();
        var votes = new List<Vote>();

        var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
            TimeSpan.FromSeconds(1),
            TestUtils.Options,
            TestUtils.PrivateKeys[1]);
        consensusContext.StateChanged += (sender, tuple) =>
        {
            if (tuple.Height == 1 && tuple.Step == ConsensusStep.EndCommit)
            {
                heightOneEndCommit.Set();
            }
        };
        consensusContext.MessagePublished += (_, eventArgs) =>
        {
            if (eventArgs.Height == 1 && eventArgs.Message is ConsensusProposalMessage proposalMsg)
            {
                proposal = proposalMsg;
                heightOneProposalSent.Set();
            }
        };

        await consensusContext.StartAsync(default);
        await heightOneProposalSent.WaitAsync();
        BlockHash proposedblockHash = Assert.IsType<BlockHash>(proposal?.BlockHash);

        votes.Add(TestUtils.CreateVote(
            TestUtils.PrivateKeys[0],
            TestUtils.Validators[0].Power,
            1,
            0,
            new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
            VoteType.PreCommit));
        votes.AddRange(Enumerable.Range(1, 3).Select(x => TestUtils.CreateVote(
            TestUtils.PrivateKeys[x],
            TestUtils.Validators[x].Power,
            1,
            0,
            proposedblockHash,
            VoteType.PreCommit)));

        foreach (var vote in votes)
        {
            consensusContext.HandleMessage(new ConsensusPreCommitMessage { PreCommit = vote });
        }

        await heightOneEndCommit.WaitAsync();

        var blockCommit = consensusContext.CurrentContext.GetBlockCommit();
        Assert.NotNull(blockCommit);
        Assert.NotEqual(votes[0], blockCommit!.Votes.First(x =>
            x.Validator.Equals(TestUtils.PrivateKeys[0].PublicKey)));

        var actualVotesWithoutInvalid
            = blockCommit.Votes.Where(x => !x.Validator.Equals(TestUtils.PrivateKeys[0].PublicKey)).ToHashSet();

        var expectedVotes
            = votes.Where(x => !x.Validator.Equals(TestUtils.PrivateKeys[0].PublicKey)).ToHashSet();

        Assert.Equal(expectedVotes, actualVotesWithoutInvalid);
    }

    [Fact(Timeout = Timeout)]
    public async Task GetVoteSetBits()
    {
        PrivateKey proposer = TestUtils.PrivateKeys[1];
        BigInteger proposerPower = TestUtils.Validators[1].Power;
        AsyncAutoResetEvent stepChanged = new AsyncAutoResetEvent();
        AsyncAutoResetEvent committed = new AsyncAutoResetEvent();
        var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
            TimeSpan.FromSeconds(1),
            TestUtils.Options,
            TestUtils.PrivateKeys[0]);
        await consensusContext.StartAsync(default);
        var block = blockChain.ProposeBlock(proposer);
        var proposal = new ProposalMetadata
        {
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
            Validator = TestUtils.PrivateKeys[2].Address,
            ValidatorPower = TestUtils.Validators[2].Power,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[2]);
        var preVote3 = new VoteMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[3].Address,
            ValidatorPower = TestUtils.Validators[3].Power,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[3]);
        consensusContext.StateChanged += (_, eventArgs) =>
        {
            if (eventArgs is { Height: 1, Step: ConsensusStep.PreCommit })
            {
                stepChanged.Set();
            }
        };
        consensusContext.CurrentContext.VoteSetModified += (_, eventArgs) =>
        {
            if (eventArgs.Flag == VoteType.PreCommit)
            {
                committed.Set();
            }
        };

        consensusContext.HandleMessage(new ConsensusProposalMessage { Proposal = proposal });
        consensusContext.HandleMessage(new ConsensusPreVoteMessage { PreVote = preVote1 });
        consensusContext.HandleMessage(new ConsensusPreVoteMessage { PreVote = preVote3 });
        await stepChanged.WaitAsync();
        await committed.WaitAsync();

        // VoteSetBits expects missing votes
        VoteSetBits voteSetBits = consensusContext.CurrentContext
        .GetVoteSetBits(0, block.BlockHash, VoteType.PreVote);
        Assert.True(
        voteSetBits.VoteBits.SequenceEqual(new[] { true, true, false, true }));
        voteSetBits = consensusContext.CurrentContext
        .GetVoteSetBits(0, block.BlockHash, VoteType.PreCommit);
        Assert.True(
        voteSetBits.VoteBits.SequenceEqual(new[] { true, false, false, false }));
    }

    [Fact(Timeout = Timeout)]
    public async Task HandleVoteSetBits()
    {
        PrivateKey proposer = TestUtils.PrivateKeys[1];
        BigInteger proposerPower = TestUtils.Validators[1].Power;
        ConsensusStep step = ConsensusStep.Default;
        var stepChanged = new AsyncAutoResetEvent();
        var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
            TimeSpan.FromSeconds(1),
            TestUtils.Options,
            TestUtils.PrivateKeys[0]);
        consensusContext.StateChanged += (_, eventArgs) =>
        {
            if (eventArgs.Step != step)
            {
                step = eventArgs.Step;
                stepChanged.Set();
            }
        };

        await consensusContext.StartAsync(default);
        var block = blockChain.ProposeBlock(proposer);
        var proposal = new ProposalMetadata
        {
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
            Validator = TestUtils.PrivateKeys[2].Address,
            ValidatorPower = TestUtils.Validators[2].Power,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[2]);
        consensusContext.HandleMessage(new ConsensusProposalMessage { Proposal = proposal });
        consensusContext.HandleMessage(new ConsensusPreVoteMessage { PreVote = preVote1 });
        consensusContext.HandleMessage(new ConsensusPreVoteMessage { PreVote = preVote2 });
        do
        {
            await stepChanged.WaitAsync();
        }
        while (step != ConsensusStep.PreCommit);

        // VoteSetBits expects missing votes
        var voteSetBits =
            new VoteSetBitsMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.PrivateKeys[1].Address,
                VoteType = VoteType.PreVote,
                VoteBits = [false, false, true, false],
            }.Sign(TestUtils.PrivateKeys[1]);
        ConsensusMessage[] votes =
    consensusContext.HandleVoteSetBits(voteSetBits).ToArray();
        Assert.True(votes.All(vote => vote is ConsensusPreVoteMessage));
        Assert.Equal(2, votes.Length);
        Assert.Equal(TestUtils.PrivateKeys[0].Address, votes[0].Validator);
        Assert.Equal(TestUtils.PrivateKeys[1].Address, votes[1].Validator);
    }

    [Fact(Timeout = Timeout)]
    public async Task HandleProposalClaim()
    {
        PrivateKey proposer = TestUtils.PrivateKeys[1];
        ConsensusStep step = ConsensusStep.Default;
        var stepChanged = new AsyncAutoResetEvent();
        var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
            TimeSpan.FromSeconds(1),
            TestUtils.Options,
            TestUtils.PrivateKeys[0]);
        consensusContext.StateChanged += (_, eventArgs) =>
        {
            if (eventArgs.Step != step)
            {
                step = eventArgs.Step;
                stepChanged.Set();
            }
        };
        await consensusContext.StartAsync(default);
        var block = blockChain.ProposeBlock(proposer);
        var proposal = new ProposalMetadata
        {
            Height = 1,
            Round = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            ValidRound = -1,
        }.Sign(proposer, block);
        consensusContext.HandleMessage(new ConsensusProposalMessage { Proposal = proposal });
        await stepChanged.WaitAsync();

        // ProposalClaim expects corresponding proposal if exists
        var proposalClaim =
        new ProposalClaimMetadata
        {
            Height = 1,
            Round = 0,
            BlockHash = block.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = TestUtils.PrivateKeys[1].Address,
        }.Sign(TestUtils.PrivateKeys[1]);
        Proposal? reply =
consensusContext.HandleProposalClaim(proposalClaim);
        Assert.NotNull(reply);
        Assert.Equal(proposal, reply);
    }
}
