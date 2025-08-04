using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Libplanet.TestUtilities.Extensions;
using Nito.AsyncEx;
using xRetry;
using Xunit.Abstractions;
using Libplanet.Tests;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextNonProposerTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task NewHeightWithLastCommit()
    {
        Proposal? proposal = null;
        var heightTwoProposalSent = new AsyncAutoResetEvent();
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockchain();
        await using var consensusService = TestUtils.CreateConsensusService(
            blockchain: blockchain,
            key: TestUtils.PrivateKeys[2],
            newHeightDelay: TimeSpan.FromSeconds(1));
        using var _1 = consensusService.BlockPropose.Subscribe(e =>
        {
            if (e.Height == 2)
            {
                proposal = e;
                heightTwoProposalSent.Set();
            }
        });
        // consensusService.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 2 && eventArgs.Message is ConsensusProposalMessage proposalMsg)
        //     {
        //         proposal = proposalMsg;
        //         heightTwoProposalSent.Set();
        //     }
        // };

        await consensusService.StartAsync(default);
        var block1 = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        var proposalMessage = TestUtils.CreateConsensusPropose(block1, TestUtils.PrivateKeys[1]);
        await consensusService.HandleMessageAsync(proposalMessage, default);
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
            var preVoteMessage = new ConsensusPreVoteMessage { PreVote = expectedVotes[i] };
            await consensusService.HandleMessageAsync(preVoteMessage, default);
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
            var preCommitMessage = new ConsensusPreCommitMessage { PreCommit = expectedVotes[i] };
            await consensusService.HandleMessageAsync(preCommitMessage, default);
        }

        await heightTwoProposalSent.WaitAsync();
        Assert.NotNull(proposal);

        var proposedBlock = proposal.Block;
        var votes = proposedBlock.PreviousCommit.Votes;
        Assert.Equal(VoteType.PreCommit, votes[0].Type);
        Assert.Equal(VoteType.PreCommit, votes[1].Type);
        Assert.Equal(VoteType.PreCommit, votes[2].Type);
        Assert.Equal(VoteType.Null, votes[3].Type);
    }

    [Fact(Timeout = Timeout)]
    public async Task HandleMessageFromHigherHeight()
    {
        Proposal? proposal = null;
        // var heightTwoStepChangedToPreVote = new AsyncAutoResetEvent();
        // var heightTwoStepChangedToPreCommit = new AsyncAutoResetEvent();
        var heightTwoStepChangedToEndCommit = new AsyncAutoResetEvent();
        var heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
        var heightThreeStepChangedToPreVote = new AsyncAutoResetEvent();
        var proposalSent = new AsyncAutoResetEvent();
        var newHeightDelay = TimeSpan.FromSeconds(1);

        var blockchain = Libplanet.Tests.TestUtils.MakeBlockchain();
        var consensusService = TestUtils.CreateConsensusService(
            blockchain: blockchain,
            key: TestUtils.PrivateKeys[2],
            newHeightDelay: newHeightDelay);
        await consensusService.StartAsync(default);

        // consensusService.StateChanged += (_, eventArgs) =>
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
        // consensusService.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Message is ConsensusProposalMessage proposalMsg)
        //     {
        //         proposal = proposalMsg;
        //         proposalSent.Set();
        //     }
        // };
        consensusService.StepChanged.Subscribe(step =>
        {
            if (consensusService.Height == 2)
            {
                if (step == ConsensusStep.PreVote)
                {
                    // heightTwoStepChangedToPreVote.Set();
                }
                else if (step == ConsensusStep.PreCommit)
                {
                    // heightTwoStepChangedToPreCommit.Set();
                }
                else if (step == ConsensusStep.EndCommit)
                {
                    heightTwoStepChangedToEndCommit.Set();
                }
            }
            else if (consensusService.Height == 3)
            {
                if (step == ConsensusStep.Propose)
                {
                    heightThreeStepChangedToPropose.Set();
                }
                else if (step == ConsensusStep.PreVote)
                {
                    heightThreeStepChangedToPreVote.Set();
                }
            }
        });

        consensusService.BlockPropose.Subscribe(e =>
        {
            if (e.Height == 2)
            {
                proposal = e;
                proposalSent.Set();
            }
        });

        Block block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        // blockchain._repository.BlockCommits.Add(TestUtils.CreateBlockCommit(blockchain.Blocks[1]));
        await proposalSent.WaitAsync();

        Assert.Equal(2, consensusService.Height);

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

            await consensusService.HandleMessageAsync(
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
                },
                default);
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

            await consensusService.HandleMessageAsync(
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
                },
                default);
        }

        await heightTwoStepChangedToEndCommit.WaitAsync();
        await blockchain.WaitUntilHeightAsync(2, default);

        var blockHeightTwo = proposal.Block;
        var blockHeightThree = blockchain.ProposeBlock(TestUtils.PrivateKeys[3]);

        // Message from higher height
        await consensusService.HandleMessageAsync(
            TestUtils.CreateConsensusPropose(blockHeightThree, TestUtils.PrivateKeys[3], 3),
            default);

        // New height started.
        await heightThreeStepChangedToPropose.WaitAsync();
        // Propose -> PreVote (message consumed)
        await heightThreeStepChangedToPreVote.WaitAsync();
        Assert.Equal(3, consensusService.Height);
        Assert.Equal(ConsensusStep.PreVote, consensusService.Step);
    }

    [Fact(Timeout = Timeout)]
    public async Task UseLastCommitCacheIfHeightContextIsEmpty()
    {
        var heightTwoProposalSent = new AsyncAutoResetEvent();
        Block? proposedBlock = null;

        var blockchain = Libplanet.Tests.TestUtils.MakeBlockchain();
        var consensusService = TestUtils.CreateConsensusService(
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[2]);
        consensusService.BlockPropose.Subscribe(e =>
        {
            if (e.Height == 2)
            {
                proposedBlock = e.Block;
                heightTwoProposalSent.Set();
            }
        });
        // consensusService.MessageConsumed += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 2 &&
        //         eventArgs.Message is ConsensusProposalMessage propose)
        //     {
        //         proposedBlock = propose!.Proposal.Block;
        //         heightTwoProposalSent.Set();
        //     }
        // };

        await consensusService.StartAsync(default);
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
        var heightOneEndCommit = new ManualResetEvent(false);
        var heightTwoProposalSent = new ManualResetEvent(false);
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockchain();
        var consensusService = TestUtils.CreateConsensusService(
            blockchain: blockchain,
            newHeightDelay: newHeightDelay,
            key: TestUtils.PrivateKeys[2]);
        // consensusService.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 1 && eventArgs.Step == ConsensusStep.EndCommit)
        //     {
        //         heightOneEndCommit.Set();
        //     }
        // };
        // consensusService.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 2 && eventArgs.Message is ConsensusProposalMessage)
        //     {
        //         heightTwoProposalSent.Set();
        //     }
        // };
        consensusService.StepChanged.Subscribe(step =>
        {
            if (consensusService.Height == 1 && step == ConsensusStep.EndCommit)
            {
                heightOneEndCommit.Set();
            }
        });

        consensusService.BlockPropose.Subscribe(e =>
        {
            if (e.Height == 2)
            {
                heightTwoProposalSent.Set();
            }
        });

        await consensusService.StartAsync(default);
        var block = blockchain.ProposeBlock(TestUtils.PrivateKeys[1]);
        await consensusService.HandleMessageAsync(
            TestUtils.CreateConsensusPropose(block, TestUtils.PrivateKeys[1]),
            default);

        await TestUtils.HandleFourPeersPreCommitMessages(
             consensusService, TestUtils.PrivateKeys[2], block.BlockHash);

        Assert.True(heightOneEndCommit.WaitOne(5000), "EndCommit not reached in time.");
        var endCommitTime = DateTimeOffset.UtcNow;

        Assert.True(heightTwoProposalSent.WaitOne(5000), "Proposal not sent in time.");
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
