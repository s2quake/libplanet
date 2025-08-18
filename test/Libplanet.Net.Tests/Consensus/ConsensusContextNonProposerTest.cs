using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Libplanet.TestUtilities.Extensions;
using Nito.AsyncEx;
using xRetry;
using Libplanet.Tests;
using Libplanet.Extensions;
using System.Reactive.Linq;
using static Libplanet.Net.Tests.TestUtils;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextNonProposerTest
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task NewHeightWithLastCommit()
    {
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[1]);
        await using var transportB = CreateTransport(Signers[2]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusServiceB = new ConsensusService(Signers[2], blockchain, transportB, options);

        var blockProposedHeight2Task = consensusServiceB.BlockProposed.WaitAsync(e => e.Height == 2);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await consensusServiceB.StartAsync(default);

        var block1 = blockchain.ProposeBlock(Signers[1]);
        var proposalMessage = new ConsensusProposalMessage
        {
            Proposal = new ProposalBuilder
            {
                Block = block1,
            }.Create(Signers[1]),
        };
        transportA.Post(transportB.Peer, proposalMessage);

        var expectedVotes = new Vote[4];

        // Peer2 sends a ConsensusVote via background process.
        // Enough votes are present to proceed even without Peer3's vote.
        for (var i = 0; i < 2; i++)
        {
            expectedVotes[i] = new VoteBuilder
            {
                Validator = Validators[i],
                Block = block1,
                Type = VoteType.PreVote,
            }.Create(PrivateKeys[i]);
            var preVoteMessage = new ConsensusPreVoteMessage { PreVote = expectedVotes[i] };
            transportA.Post(transportB.Peer, preVoteMessage);
        }

        // Peer2 sends a ConsensusCommit via background process.
        // Enough votes are present to proceed even without Peer3's vote.
        for (var i = 0; i < 2; i++)
        {
            expectedVotes[i] = new VoteBuilder
            {
                Validator = Validators[i],
                Block = block1,
                Type = VoteType.PreCommit,
            }.Create(PrivateKeys[i]);
            var preCommitMessage = new ConsensusPreCommitMessage { PreCommit = expectedVotes[i] };
            transportA.Post(transportB.Peer, preCommitMessage);
        }

        var proposal = await blockProposedHeight2Task.WaitAsync(TimeSpan.FromSeconds(5));
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
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[1]);
        await using var transportB = CreateTransport(Signers[2]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusServiceB = new ConsensusService(Signers[2], blockchain, transportB, options);
        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusServiceB.StartAsync();

        blockchain.ProposeAndAppend(Signers[1]);

        var proposal = await consensusServiceB.BlockProposed.WaitAsync(e => e.Height == 2);
        Assert.Equal(2, consensusServiceB.Height);

        for (var i = 0; i < Validators.Count; i++)
        {
            if (i == 2)
            {
                // Peer2 will send a ConsensusVote by handling the ConsensusPropose message.
                continue;
            }
            var vote = new VoteBuilder
            {
                Validator = Validators[i],
                Block = proposal.Block,
                Type = VoteType.PreVote,
            }.Create(PrivateKeys[i]);
            var message = new ConsensusPreVoteMessage { PreVote = vote };
            transportA.Post(transportB.Peer, message);
        }

        for (var i = 0; i < Validators.Count; i++)
        {
            if (i == 2)
            {
                // Peer2 will send a ConsensusCommit by handling the ConsensusVote message.
                continue;
            }

            var vote = new VoteBuilder
            {
                Validator = Validators[i],
                Block = proposal.Block,
                Type = VoteType.PreCommit,
            }.Create(PrivateKeys[i]);
            var message = new ConsensusPreCommitMessage { PreCommit = vote };
            transportA.Post(transportB.Peer, message);
        }

        await TaskUtility.WhenAll(
            timeout: WaitTimeout,
            consensusServiceB.StepChanged.WaitAsync(e => e == ConsensusStep.EndCommit && consensusServiceB.Height == 2),
            blockchain.TipChanged.WaitAsync(e => e.Tip.Height == 2));

        var block3 = blockchain.ProposeBlock(Signers[3]);
        var proposal3 = new ProposalBuilder
        {
            Block = block3,
        }.Create(Signers[3]);
        var proposalMessage3 = new ConsensusProposalMessage { Proposal = proposal3 };

        // Message from higher height
        transportA.Post(transportB.Peer, proposalMessage3);

        await TaskUtility.WhenAll(
            timeout: WaitTimeout,
            consensusServiceB.HeightChanged.WaitAsync(e => e == 3),
            consensusServiceB.BlockProposed.WaitAsync(e => e.Height == 3),
            consensusServiceB.StepChanged.WaitAsync(e => e == ConsensusStep.PreVote));

        Assert.Equal(3, consensusServiceB.Height);
        Assert.Equal(ConsensusStep.PreVote, consensusServiceB.Step);
    }

    [Fact(Timeout = Timeout)]
    public async Task UseLastCommitCacheIfHeightContextIsEmpty()
    {
        var blockchain = MakeBlockchain();
        await using var transport = CreateTransport(Signers[2]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[2], blockchain, transport, options);

        await transport.StartAsync();
        await consensusService.StartAsync();

        var (_, blockCommit) = blockchain.ProposeAndAppend(Signers[2]);
        var proposal = await consensusService.BlockProposed.WaitAsync(e => e.Height == 2, WaitTimeout);

        // Context for height #2 where node #2 is the proposer is automatically started
        // by watching blockchain's Tip.
        Assert.Equal(blockCommit, proposal.Block.PreviousCommit);
    }

    // Retry: This calculates delta time.
    [RetryFact(10, Timeout = Timeout)]
    public async Task NewHeightDelay()
    {
        var newHeightDelay = TimeSpan.FromSeconds(1);
        // The maximum error margin. (macos-netcore-test)
        var timeError = 500;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[1]);
        await using var transportB = CreateTransport(Signers[2]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = newHeightDelay,
        };
        await using var consensusServiceB = new ConsensusService(Signers[2], blockchain, transportB, options: options);

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusServiceB.StartAsync();
        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);
        var proposalMessage = new ConsensusProposalMessage { Proposal = proposal };
        transportA.Post(transportB.Peer, proposalMessage);

        for (var i = 0; i < Validators.Count; i++)
        {
            if (i == 2)
            {
                continue;
            }

            var vote = new VoteBuilder
            {
                Validator = Validators[i],
                Block = block,
                Type = VoteType.PreCommit,
            }.Create(PrivateKeys[i]);
            var preCommitMessage = new ConsensusPreCommitMessage { PreCommit = vote };
            transportA.Post(transportB.Peer, preCommitMessage);
        }

        var endCommitStep1Task = consensusServiceB.StepChanged.WaitAsync(
            e => e == ConsensusStep.EndCommit && consensusServiceB.Height == 1);
        var proposeStep2Task = consensusServiceB.StepChanged.WaitAsync(
            e => e == ConsensusStep.Propose && consensusServiceB.Height == 2);

        await endCommitStep1Task.WaitAsync(WaitTimeout);
        var endCommitTime = DateTimeOffset.UtcNow;

        await proposeStep2Task.WaitAsync(WaitTimeout);
        var proposeTime = DateTimeOffset.UtcNow;

        // Check new height delay; slight margin of error is allowed as delay task
        // is run asynchronously from context events.
        Assert.True(
            (proposeTime - endCommitTime - newHeightDelay).Duration() <
                TimeSpan.FromMilliseconds(timeError));
    }
}
