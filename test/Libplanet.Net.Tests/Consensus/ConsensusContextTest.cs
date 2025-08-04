using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
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
        var blockchain = TestUtils.MakeBlockchain();
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[3]);
        await consensusService.StartAsync(default);

        AsyncAutoResetEvent heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
        AsyncAutoResetEvent heightThreeStepChangedToEndCommit = new AsyncAutoResetEvent();
        AsyncAutoResetEvent heightFourStepChangedToPropose = new AsyncAutoResetEvent();
        AsyncAutoResetEvent onTipChangedToThree = new AsyncAutoResetEvent();
        // consensusService.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 3 && eventArgs.Step == ConsensusStep.Propose)
        //     {
        //         heightThreeStepChangedToPropose.Set();
        //     }
        //     else if (eventArgs.Height == 3 && eventArgs.Step == ConsensusStep.EndCommit)
        //     {
        //         heightThreeStepChangedToEndCommit.Set();
        //     }
        //     else if (eventArgs.Height == 4 && eventArgs.Step == ConsensusStep.Propose)
        //     {
        //         heightFourStepChangedToPropose.Set();
        //     }
        // };
        // consensusService.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Message is ConsensusProposalMessage proposalMsg)
        //     {
        //         proposal = proposalMsg;
        //         proposalMessageSent.Set();
        //     }
        // };
        using var _ = blockchain.TipChanged.Subscribe(eventArgs =>
        {
            if (eventArgs.Tip.Height == 3L)
            {
                onTipChangedToThree.Set();
            }
        });

        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(TestUtils.PrivateKeys[2]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Equal(2, blockchain.Tip.Height);

        // Wait for context of height 3 to start.
        await heightThreeStepChangedToPropose.WaitAsync();
        Assert.Equal(3, consensusService.Height);

        // Cannot call NewHeight() with invalid heights.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await consensusService.NewHeightAsync(2, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await consensusService.NewHeightAsync(3, default));

        await proposalMessageSent.WaitAsync();
        BlockHash proposedblockHash = Assert.IsType<BlockHash>(proposal?.BlockHash);

        await consensusService.HandleMessageAsync(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[0],
                    TestUtils.Validators[0].Power,
                    3,
                    0,
                    hash: proposedblockHash,
                    flag: VoteType.PreCommit)
            },
            default);
        await consensusService.HandleMessageAsync(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[1],
                    TestUtils.Validators[1].Power,
                    3,
                    0,
                    hash: proposedblockHash,
                    flag: VoteType.PreCommit)
            },
            default);
        await consensusService.HandleMessageAsync(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    3,
                    0,
                    hash: proposedblockHash,
                    flag: VoteType.PreCommit)
            },
            default);

        // Waiting for commit.
        await heightThreeStepChangedToEndCommit.WaitAsync();
        await onTipChangedToThree.WaitAsync();
        Assert.Equal(3, blockchain.Tip.Height);

        // Next height starts normally.
        await heightFourStepChangedToPropose.WaitAsync();
        Assert.Equal(4, consensusService.Height);
        Assert.Equal(0, consensusService.Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task Ctor()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[1]);

        Assert.Equal(ConsensusStep.Default, consensusService.Step);
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(-1, consensusService.Round);
    }

    [Fact]
    public async Task CannotStartTwice()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[1]);
        await consensusService.StartAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await consensusService.StartAsync(default));
    }

    [Fact(Timeout = Timeout)]
    public async Task NewHeightWhenTipChanged()
    {
        var newHeightDelay = TimeSpan.FromSeconds(1);
        var blockchain = TestUtils.MakeBlockchain();
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            blockchain: blockchain,
            newHeightDelay: newHeightDelay,
            key: TestUtils.PrivateKeys[1]);
        await consensusService.StartAsync(default);

        Assert.Equal(1, consensusService.Height);
        Block block = blockchain.ProposeBlock(new PrivateKey());
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));
        Assert.Equal(1, consensusService.Height);
        await Task.Delay(newHeightDelay + TimeSpan.FromSeconds(1));
        Assert.Equal(2, consensusService.Height);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreMessagesFromLowerHeight()
    {
        var blockchain = TestUtils.MakeBlockchain();
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[1]);
        await consensusService.StartAsync(default);
        Assert.True(consensusService.Height == 1);
        Assert.False(await consensusService.HandleMessageAsync(
            TestUtils.CreateConsensusPropose(blockchain.ProposeBlock(TestUtils.PrivateKeys[0]), TestUtils.PrivateKeys[0], 0),
            default));
    }

    [Fact(Timeout = Timeout)]
    public async Task VoteSetGetOnlyProposeCommitHash()
    {
        ConsensusProposalMessage? proposal = null;
        var heightOneProposalSent = new AsyncAutoResetEvent();
        var heightOneEndCommit = new AsyncAutoResetEvent();
        var votes = new List<Vote>();

        var blockchain = TestUtils.MakeBlockchain();
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[1]);
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
            await consensusService.HandleMessageAsync(new ConsensusPreCommitMessage { PreCommit = vote }, default);
        }

        await heightOneEndCommit.WaitAsync();

        var blockCommit = consensusService.Consensus.GetBlockCommit();
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
        var blockchain = TestUtils.MakeBlockchain();
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[0]);
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

        await consensusService.HandleMessageAsync(new ConsensusProposalMessage { Proposal = proposal }, default);
        await consensusService.HandleMessageAsync(new ConsensusPreVoteMessage { PreVote = preVote1 }, default);
        await consensusService.HandleMessageAsync(new ConsensusPreVoteMessage { PreVote = preVote3 }, default);
        await stepChanged.WaitAsync();
        await committed.WaitAsync();

        // VoteSetBits expects missing votes
        VoteSetBits voteSetBits = consensusService.Consensus
        .GetVoteSetBits(0, block.BlockHash, VoteType.PreVote);
        Assert.True(
        voteSetBits.VoteBits.SequenceEqual(new[] { true, true, false, true }));
        voteSetBits = consensusService.Consensus
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
        var blockchain = TestUtils.MakeBlockchain();
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[0]);
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
            Validator = TestUtils.PrivateKeys[2].Address,
            ValidatorPower = TestUtils.Validators[2].Power,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.PrivateKeys[2]);
        await consensusService.HandleMessageAsync(new ConsensusProposalMessage { Proposal = proposal }, default);
        await consensusService.HandleMessageAsync(new ConsensusPreVoteMessage { PreVote = preVote1 }, default);
        await consensusService.HandleMessageAsync(new ConsensusPreVoteMessage { PreVote = preVote2 }, default);
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
    consensusService.HandleVoteSetBits(voteSetBits).ToArray();
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
        var blockchain = TestUtils.MakeBlockchain();
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[0]);
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
        await consensusService.HandleMessageAsync(new ConsensusProposalMessage { Proposal = proposal }, default);
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
consensusService.HandleProposalClaim(proposalClaim);
        Assert.NotNull(reply);
        Assert.Equal(proposal, reply);
    }
}
