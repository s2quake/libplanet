using System.Reactive.Linq;
using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class DuplicateVoteEvidenceTest(ITestOutputHelper output)
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task Evidence_WithDuplicateVotes_Test()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[3]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[3], blockchain, transportB, options);

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
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[3]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[3], blockchain, transportB, options);
        var proposed3Task = consensusService.BlockProposed.WaitAsync(e => e.Height == 3);

        blockchain.ProposeAndAppend(Signers[1]);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await consensusService.StartAsync(cancellationToken);

        blockchain.ProposeAndAppend(Signers[2]);

        var proposal3 = await proposed3Task.WaitAsync(cancellationToken);
        var invalidBlockHash3 = RandomUtility.BlockHash(random);

        transportA.PostPreCommit(transportB.Peer, 0, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 0, invalidBlockHash3, height: 4);
        transportA.PostPreCommit(transportB.Peer, 1, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 2, proposal3.Block);

        await consensusService.HeightChanged.WaitAsync(e => e == 4, cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreDifferentRoundVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[3]);
        await using var consensusService = new ConsensusService(Signers[3], blockchain, transportB);
        var proposed3Task = consensusService.BlockProposed.WaitAsync(e => e.Height == 3);

        blockchain.ProposeAndAppend(Signers[1]);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await consensusService.StartAsync(cancellationToken);

        blockchain.ProposeAndAppend(Signers[2]);

        var proposal3 = await proposed3Task.WaitAsync(cancellationToken);
        var invalidBlockHash3 = RandomUtility.BlockHash(random);

        transportA.PostPreCommit(transportB.Peer, 0, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 0, invalidBlockHash3, height: 3, round: 1);
        transportA.PostPreCommit(transportB.Peer, 1, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 2, proposal3.Block);

        await consensusService.HeightChanged.WaitAsync(e => e == 4, cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreDifferentFlagVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[3]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[3], blockchain, transportB, options);
        var proposed3Task = consensusService.BlockProposed.WaitAsync(e => e.Height == 3);

        blockchain.ProposeAndAppend(Signers[1]);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await consensusService.StartAsync(cancellationToken);

        blockchain.ProposeAndAppend(Signers[2]);

        var proposal3 = await proposed3Task.WaitAsync(cancellationToken);
        var invalidBlockHash3 = RandomUtility.BlockHash(random);

        transportA.PostPreCommit(transportB.Peer, 0, proposal3.Block);
        transportA.PostPreVote(transportB.Peer, 0, invalidBlockHash3, height: 3);
        transportA.PostPreCommit(transportB.Peer, 1, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 2, proposal3.Block);

        await consensusService.HeightChanged.WaitAsync(e => e == 4, cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreSameBlockHashVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[3]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[3], blockchain, transportB, options);
        var proposed3Task = consensusService.BlockProposed.WaitAsync(e => e.Height == 3);

        blockchain.ProposeAndAppend(Signers[1]);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await consensusService.StartAsync(cancellationToken);

        blockchain.ProposeAndAppend(Signers[2]);

        var proposal3 = await proposed3Task.WaitAsync(cancellationToken);

        transportA.PostPreCommit(transportB.Peer, 0, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 0, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 1, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 2, proposal3.Block);

        await consensusService.HeightChanged.WaitAsync(e => e == 4, cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IgnoreNillVote()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[3]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[3], blockchain, transportB, options);
        var proposed3Task = consensusService.BlockProposed.WaitAsync(e => e.Height == 3);

        blockchain.ProposeAndAppend(Signers[1]);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await consensusService.StartAsync(cancellationToken);

        blockchain.ProposeAndAppend(Signers[2]);

        var proposal3 = await proposed3Task.WaitAsync(cancellationToken);

        transportA.PostPreCommit(transportB.Peer, 0, proposal3.Block);
        transportA.PostNilPreCommit(transportB.Peer, 0, height: 3);
        transportA.PostPreCommit(transportB.Peer, 1, proposal3.Block);
        transportA.PostPreCommit(transportB.Peer, 2, proposal3.Block);

        await consensusService.HeightChanged.WaitAsync(e => e == 4, cancellationToken);

        Assert.Empty(blockchain.Blocks[3].Evidences);
    }
}
