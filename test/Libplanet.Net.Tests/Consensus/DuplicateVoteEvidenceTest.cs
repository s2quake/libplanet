#pragma warning disable S125
using Libplanet.Net.Messages;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Serilog;

namespace Libplanet.Net.Tests.Consensus;

public class DuplicateVoteEvidenceTest
{
    private const int Timeout = 30000;
    private readonly ILogger _logger;

    public DuplicateVoteEvidenceTest(ITestOutputHelper output)
    {
        const string outputTemplate =
            "{Timestamp:HH:mm:ss:ffffffZ} - {Message} {Exception}";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            // .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
            .CreateLogger()
            .ForContext<ConsensusContextTest>();

        _logger = Log.ForContext<ConsensusContextTest>();
    }

    [Fact(Timeout = Timeout)]
    public async Task Evidence_WithDuplicateVotes_Test()
    {
        var privateKeys = TestUtils.PrivateKeys;
        var blockchain = TestUtils.MakeBlockchain();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: privateKeys[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: new CancellationTokenSource(Timeout).Token);
        var consensusProposalMsgAt7Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 7,
            cancellationToken: new CancellationTokenSource(Timeout).Token);
        var block = blockchain.ProposeBlock(privateKeys[1]);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(privateKeys[2]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[1].Address,
                    ValidatorPower = TestUtils.Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[2].Address,
                    ValidatorPower = TestUtils.Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: new CancellationTokenSource(Timeout).Token);

        Assert.Single(blockchain.PendingEvidence);
        Assert.Equal(4, consensusService.Height);
        Assert.Equal(0, consensusService.Round);

        blockCommit = blockchain.BlockCommits[blockchain.Tip.BlockHash];
        block = blockchain.ProposeBlock(privateKeys[0]);
        blockCommit = TestUtils.CreateBlockCommit(block);
        blockchain.Append(block, blockCommit);

        block = blockchain.ProposeBlock(privateKeys[1]);
        blockCommit = TestUtils.CreateBlockCommit(block);
        blockchain.Append(block, blockCommit);

        block = blockchain.ProposeBlock(privateKeys[2]);
        blockCommit = TestUtils.CreateBlockCommit(block);
        blockchain.Append(block, blockCommit);

        await consensusProposalMsgAt7Task;
        var consensusProposalMsgAt7 = consensusProposalMsgAt7Task.Result;
        Assert.NotNull(consensusProposalMsgAt3?.BlockHash);
        var actualBlock = consensusProposalMsgAt7.Proposal.Block;
        Assert.Single(actualBlock.Evidences);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreDifferentHeightVote()
    {
        var privateKeys = TestUtils.PrivateKeys;
        var blockchain = TestUtils.MakeBlockchain();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport: transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: privateKeys[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: new CancellationTokenSource(Timeout).Token);
        var block = blockchain.ProposeBlock(privateKeys[1]);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(privateKeys[2]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 4,
                    BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[1].Address,
                    ValidatorPower = TestUtils.Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[2].Address,
                    ValidatorPower = TestUtils.Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: new CancellationTokenSource(Timeout).Token);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreDifferentRoundVote()
    {
        var privateKeys = TestUtils.PrivateKeys;
        var blockchain = TestUtils.MakeBlockchain();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: new CancellationTokenSource(Timeout).Token);
        var block = blockchain.ProposeBlock(privateKeys[1]);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(privateKeys[2]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    Round = 1,
                    BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    Type = VoteType.PreCommit
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[1].Address,
                    ValidatorPower = TestUtils.Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[2].Address,
                    ValidatorPower = TestUtils.Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: new CancellationTokenSource(Timeout).Token);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreDifferentFlagVote()
    {
        var privateKeys = TestUtils.PrivateKeys;
        var blockchain = TestUtils.MakeBlockchain();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport: transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: privateKeys[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: new CancellationTokenSource(Timeout).Token);
        var block = blockchain.ProposeBlock(privateKeys[1]);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(privateKeys[2]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size)),
                    Type = VoteType.PreVote,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[1].Address,
                    ValidatorPower = TestUtils.Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[2].Address,
                    ValidatorPower = TestUtils.Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: new CancellationTokenSource(Timeout).Token);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreSameBlockHashVote()
    {
        var privateKeys = TestUtils.PrivateKeys;
        var blockchain = TestUtils.MakeBlockchain();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: new CancellationTokenSource(Timeout).Token);
        var block = blockchain.ProposeBlock(privateKeys[1]);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(privateKeys[2]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[1].Address,
                    ValidatorPower = TestUtils.Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[2].Address,
                    ValidatorPower = TestUtils.Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: new CancellationTokenSource(Timeout).Token);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreNillVote()
    {
        var privateKeys = TestUtils.PrivateKeys;
        var blockchain = TestUtils.MakeBlockchain();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: privateKeys[3]);

        var consensusProposalMsgAt3Task = consensusService.WaitUntilPublishedAsync<ConsensusProposalMessage>(
            height: 3,
            cancellationToken: new CancellationTokenSource(Timeout).Token);
        var block = blockchain.ProposeBlock(privateKeys[1]);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        await consensusService.StartAsync(default);
        blockchain.Append(block, blockCommit);
        block = blockchain.ProposeBlock(privateKeys[2]);
        blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        await consensusProposalMsgAt3Task;
        var consensusProposalMsgAt3 = consensusProposalMsgAt3Task.Result;
        var blockHash = consensusProposalMsgAt3.BlockHash;

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[0].Address,
                    ValidatorPower = TestUtils.Validators[0].Power,
                    Height = 3,
                    BlockHash = default,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[0])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[1].Address,
                    ValidatorPower = TestUtils.Validators[1].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[1])
            });
        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[2].Address,
                    ValidatorPower = TestUtils.Validators[2].Power,
                    Height = 3,
                    BlockHash = blockHash,
                    Type = VoteType.PreCommit,
                }.Sign(privateKeys[2])
            });

        await consensusService.WaitUntilAsync(
            height: 4,
            cancellationToken: new CancellationTokenSource(Timeout).Token);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }
}
