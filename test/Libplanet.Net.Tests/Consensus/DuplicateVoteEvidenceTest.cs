using Libplanet.Data;
using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class DuplicateVoteEvidenceTest
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Evidence_WithDuplicateVotes_Test()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[3]);
        await using var consensusService = new ConsensusService(Signers[3], blockchain, transportB);

        var proposed3Task = consensusService.BlockProposed.WaitAsync(e => e.Height == 3);
        var proposed7Task = consensusService.BlockProposed.WaitAsync(e => e.Height == 7);

        _ = blockchain.ProposeAndAppend(Signers[1]);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await consensusService.StartAsync(cancellationToken);

        _ = blockchain.ProposeAndAppend(Signers[2]);

        var proposal3 = await proposed3Task.WaitAsync(cancellationToken);
        foreach (var i in new int[] { 0, 2, 3 })
        {
            var message = new ConsensusPreCommitMessage
            {
                PreCommit = new VoteBuilder
                {
                    Validator = Validators[i],
                    Block = proposal3.Block,
                    Type = VoteType.PreCommit,
                }.Create(Signers[i])
            };
            transportA.Post(transportB.Peer, message);
        }
        await Task.Delay(100, cancellationToken);

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });

        await consensusService.HeightChanged.WaitAsync(e => e == 4, cancellationToken);

        var kv = Assert.Single(blockchain.PendingEvidence);
        Assert.Equal(4, consensusService.Height);
        Assert.Equal(0, consensusService.Round);
        blockchain.PendingEvidence.Remove(kv.Key);

        blockchain.ProposeAndAppend(Signers[0]);
        blockchain.ProposeAndAppend(Signers[1]);
        blockchain.ProposeAndAppend(Signers[2]);

        blockchain.PendingEvidence.Add(kv.Value);
        var proposal7 = await proposed7Task.WaitAsync(cancellationToken);

        var actualBlock = proposal7.Block;
        Assert.Single(actualBlock.Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreDifferentHeightVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var Signers = PrivateKeys;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transport: transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: Signers[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: cancellationToken);
        var block = blockchain.ProposeBlock(Signers[1]);
        var blockCommit = CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(Signers[2]);
        blockchain.Append(block, CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 4,
                    BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[1].Address,
                    ValidatorPower = Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreDifferentRoundVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var Signers = PrivateKeys;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: PrivateKeys[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: cancellationToken);
        var block = blockchain.ProposeBlock(Signers[1]);
        var blockCommit = CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(Signers[2]);
        blockchain.Append(block, CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    Round = 1,
                    BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    Type = VoteType.PreCommit
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[1].Address,
                    ValidatorPower = Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreDifferentFlagVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var Signers = PrivateKeys;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transport: transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: Signers[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: cancellationToken);
        var block = blockchain.ProposeBlock(Signers[1]);
        var blockCommit = CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(Signers[2]);
        blockchain.Append(block, CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    Type = VoteType.PreVote,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[1].Address,
                    ValidatorPower = Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreSameBlockHashVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var Signers = PrivateKeys;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: PrivateKeys[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: cancellationToken);
        var block = blockchain.ProposeBlock(Signers[1]);
        var blockCommit = CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(Signers[2]);
        blockchain.Append(block, CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[1].Address,
                    ValidatorPower = Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreNillVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var Signers = PrivateKeys;
        var blockchain = MakeBlockchain();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var consensusService = CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: Signers[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: cancellationToken);
        var block = blockchain.ProposeBlock(Signers[1]);
        var blockCommit = CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(Signers[2]);
        blockchain.Append(block, CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[0].Address,
                    ValidatorPower = Validators[0].Power,
                    Height = 3,
                    BlockHash = default,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[1].Address,
                    ValidatorPower = Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = Validators[2].Address,
                    ValidatorPower = Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(Signers[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }
}
