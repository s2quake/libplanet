using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.TestUtilities.Extensions;
using Nito.AsyncEx;
using Serilog;
using xRetry;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextNonProposerTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task NewHeightWithLastCommit()
    {
        var tipChanged = new AsyncAutoResetEvent();
        ConsensusProposalMessage? proposal = null;
        var heightTwoProposalSent = new AsyncAutoResetEvent();

        var blockchain = TestUtils.CreateBlockchain();
        await using var consensusReactor = TestUtils.CreateConsensusReactor(
            blockchain: blockchain,
            key: TestUtils.PrivateKeys[2],
            newHeightDelay: TimeSpan.FromSeconds(1));
        using var _ = blockchain.TipChanged.Subscribe(e => tipChanged.Set());
        // consensusReactor.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 2 && eventArgs.Message is ConsensusProposalMessage proposalMsg)
        //     {
        //         proposal = proposalMsg;
        //         heightTwoProposalSent.Set();
        //     }
        // };

        await consensusReactor.StartAsync(default);
        var block1 = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        consensusReactor.HandleMessage(
            TestUtils.CreateConsensusPropose(block1, TestUtils.PrivateKeys[1]));
        var expectedVotes = new Vote[4];

        // Peer2 sends a ConsensusVote via background process.
        // Enough votes are present to proceed even without Peer3's vote.
        for (var i = 0; i < 2; i++)
        {
            expectedVotes[i] = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block1.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.Validators[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreVote,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensusReactor.HandleMessage(new ConsensusPreVoteMessage { PreVote = expectedVotes[i] });
        }

        // Peer2 sends a ConsensusCommit via background process.
        // Enough votes are present to proceed even without Peer3's vote.
        for (int i = 0; i < 2; i++)
        {
            expectedVotes[i] = new VoteMetadata
            {
                Height = 1,
                Round = 0,
                BlockHash = block1.BlockHash,
                Timestamp = DateTimeOffset.UtcNow,
                Validator = TestUtils.Validators[i].Address,
                ValidatorPower = TestUtils.Validators[i].Power,
                Type = VoteType.PreCommit,
            }.Sign(TestUtils.PrivateKeys[i]);
            consensusReactor.HandleMessage(new ConsensusPreCommitMessage { PreCommit = expectedVotes[i] });
        }

        await heightTwoProposalSent.WaitAsync();
        Assert.NotNull(proposal);

        Block proposedBlock = proposal!.Proposal.Block;
        ImmutableArray<Vote> votes = proposedBlock.PreviousCommit.Votes is { } vs
            ? vs
            : throw new NullReferenceException();
        Assert.Equal(VoteType.PreCommit, votes[0].Type);
        Assert.Equal(VoteType.PreCommit, votes[1].Type);
        Assert.Equal(VoteType.PreCommit, votes[2].Type);
        Assert.Equal(VoteType.Null, votes[3].Type);
    }

    [Fact(Timeout = Timeout)]
    public async Task HandleMessageFromHigherHeight()
    {
        ConsensusProposalMessage? proposal = null;
        var heightTwoStepChangedToPreVote = new AsyncAutoResetEvent();
        var heightTwoStepChangedToPreCommit = new AsyncAutoResetEvent();
        var heightTwoStepChangedToEndCommit = new AsyncAutoResetEvent();
        var heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
        var heightThreeStepChangedToPreVote = new AsyncAutoResetEvent();
        var proposalSent = new AsyncAutoResetEvent();
        var newHeightDelay = TimeSpan.FromSeconds(1);

        var blockchain = TestUtils.CreateBlockchain();
        var consensusReactor = TestUtils.CreateConsensusReactor(
            blockchain: blockchain,
            key: TestUtils.PrivateKeys[2],
            newHeightDelay: newHeightDelay);
        await consensusReactor.StartAsync(default);

        // consensusReactor.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 2)
        //     {
        //         if (eventArgs.Step == ConsensusStep.PreVote)
        //         {
        //             heightTwoStepChangedToPreVote.Set();
        //         }
        //         else if (eventArgs.Step == ConsensusStep.PreCommit)
        //         {
        //             heightTwoStepChangedToPreCommit.Set();
        //         }
        //         else if (eventArgs.Step == ConsensusStep.EndCommit)
        //         {
        //             heightTwoStepChangedToEndCommit.Set();
        //         }
        //     }
        //     else if (eventArgs.Height == 3)
        //     {
        //         if (eventArgs.Step == ConsensusStep.Propose)
        //         {
        //             heightThreeStepChangedToPropose.Set();
        //         }
        //         else if (eventArgs.Step == ConsensusStep.PreVote)
        //         {
        //             heightThreeStepChangedToPreVote.Set();
        //         }
        //     }
        // };
        // consensusReactor.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Message is ConsensusProposalMessage proposalMsg)
        //     {
        //         proposal = proposalMsg;
        //         proposalSent.Set();
        //     }
        // };

        Block block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        // blockchain._repository.BlockCommits.Add(TestUtils.CreateBlockCommit(blockchain.Blocks[1]));
        await proposalSent.WaitAsync();

        Assert.Equal(2, consensusReactor.Height);

        if (proposal is null)
        {
            throw new Exception("Proposal is null.");
        }

        foreach ((PrivateKey privateKey, BigInteger power)
                 in TestUtils.PrivateKeys.Zip(
                     TestUtils.Validators.Select(v => v.Power),
                     (first, second) => (first, second)))
        {
            if (privateKey == TestUtils.PrivateKeys[2])
            {
                // Peer2 will send a ConsensusVote by handling the ConsensusPropose message.
                continue;
            }

            consensusReactor.HandleMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = new VoteMetadata
                    {
                        Height = 2,
                        Round = 0,
                        BlockHash = proposal!.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = privateKey.Address,
                        ValidatorPower = power,
                        Type = VoteType.PreVote,
                    }.Sign(privateKey)
                });
        }

        foreach ((PrivateKey privateKey, BigInteger power)
                 in TestUtils.PrivateKeys.Zip(
                     TestUtils.Validators.Select(v => v.Power),
                     (first, second) => (first, second)))
        {
            if (privateKey == TestUtils.PrivateKeys[2])
            {
                // Peer2 will send a ConsensusCommit by handling the ConsensusVote message.
                continue;
            }

            consensusReactor.HandleMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = new VoteMetadata
                    {
                        Height = 2,
                        Round = 0,
                        BlockHash = proposal!.BlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = privateKey.Address,
                        ValidatorPower = power,
                        Type = VoteType.PreCommit,
                    }.Sign(privateKey)
                });
        }

        await heightTwoStepChangedToEndCommit.WaitAsync();

        var blockHeightTwo = proposal.Proposal.Block;
        var blockHeightThree = blockchain.ProposeBlock(TestUtils.PrivateKeys[3]);

        // Message from higher height
        consensusReactor.HandleMessage(
            TestUtils.CreateConsensusPropose(blockHeightThree, TestUtils.PrivateKeys[3], 3));

        // New height started.
        await heightThreeStepChangedToPropose.WaitAsync();
        // Propose -> PreVote (message consumed)
        await heightThreeStepChangedToPreVote.WaitAsync();
        Assert.Equal(3, consensusReactor.Height);
        Assert.Equal(ConsensusStep.PreVote, consensusReactor.Step);
    }

    [Fact(Timeout = Timeout)]
    public async Task UseLastCommitCacheIfHeightContextIsEmpty()
    {
        var heightTwoProposalSent = new AsyncAutoResetEvent();
        Block? proposedBlock = null;

        var blockchain = TestUtils.CreateBlockchain();
        var consensusReactor = TestUtils.CreateConsensusReactor(
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[2]);
        // consensusReactor.MessageConsumed += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 2 &&
        //         eventArgs.Message is ConsensusProposalMessage propose)
        //     {
        //         proposedBlock = propose!.Proposal.Block;
        //         heightTwoProposalSent.Set();
        //     }
        // };

        await consensusReactor.StartAsync(default);
        Block block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var createdLastCommit = TestUtils.CreateBlockCommit(block);
        blockchain.Append(block, createdLastCommit);

        // Context for height #2 where node #2 is the proposer is automatically started
        // by watching blockchain's Tip.
        await heightTwoProposalSent.WaitAsync();
        Assert.NotNull(proposedBlock);
        Assert.Equal(2, proposedBlock!.Height);
        Assert.Equal(createdLastCommit, proposedBlock!.PreviousCommit);
    }

    // Retry: This calculates delta time.
    [RetryFact(10, Timeout = Timeout)]
    public async Task NewHeightDelay()
    {
        var newHeightDelay = TimeSpan.FromSeconds(1);
        // The maximum error margin. (macos-netcore-test)
        var timeError = 500;
        var heightOneEndCommit = new AsyncAutoResetEvent();
        var heightTwoProposalSent = new AsyncAutoResetEvent();
        var blockchain = TestUtils.CreateBlockchain();
        var consensusReactor = TestUtils.CreateConsensusReactor(
            blockchain: blockchain,
            newHeightDelay: newHeightDelay,
            key: TestUtils.PrivateKeys[2]);
        // consensusReactor.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 1 && eventArgs.Step == ConsensusStep.EndCommit)
        //     {
        //         heightOneEndCommit.Set();
        //     }
        // };
        // consensusReactor.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 2 && eventArgs.Message is ConsensusProposalMessage)
        //     {
        //         heightTwoProposalSent.Set();
        //     }
        // };

        await consensusReactor.StartAsync(default);
        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        consensusReactor.HandleMessage(
            TestUtils.CreateConsensusPropose(block, TestUtils.PrivateKeys[1]));

        TestUtils.HandleFourPeersPreCommitMessages(
             consensusReactor, TestUtils.PrivateKeys[2], block.BlockHash);

        await heightOneEndCommit.WaitAsync();
        var endCommitTime = DateTimeOffset.UtcNow;

        await heightTwoProposalSent.WaitAsync();
        var proposeTime = DateTimeOffset.UtcNow;
        var difference = proposeTime - endCommitTime;

        // _logger.Debug("Difference: {Difference}", difference);
        // Check new height delay; slight margin of error is allowed as delay task
        // is run asynchronously from context events.
        Assert.True(
            ((proposeTime - endCommitTime) - newHeightDelay).Duration() <
                TimeSpan.FromMilliseconds(timeError));
    }
}
