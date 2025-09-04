using Libplanet.Net.Consensus;
using Libplanet.Types;
using Libplanet.Extensions;
using System.Reactive.Linq;
using static Libplanet.Net.Tests.TestUtils;
using Libplanet.TestUtilities;

namespace Libplanet.Net.Tests;

public partial class SwarmTest
{
    [Fact(Timeout = Timeout)]
    public async Task DuplicateVote_Test()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var signers = Libplanet.Tests.TestUtils.Signers.ToArray();
        var count = signers.Length;
        var transports = new ServiceCollection<ITransport>(signers.Select(item => CreateTransport(item)));
        var blockchains = transports.Select(item => new Blockchain(genesisBlock)).ToArray();

        var peers = transports.Select(item => item.Peer);
        var serviceOptions = Enumerable.Range(0, count).Select(i =>
            new ConsensusServiceOptions
            {
                KnownPeers = [.. peers.Where(item => item.Address != transports[i].Peer.Address)],
                Workers = 100,
                TargetBlockInterval = TimeSpan.FromSeconds(4),
                ConsensusOptions = new ConsensusOptions(),
            }).ToArray();
        var services = new ServiceCollection<ConsensusService>(serviceOptions.Select((options, i) =>
        {
            return new ConsensusService(signers[i], blockchains[i], transports[i], options);
        }));

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        var consensusService = services[0];
        var round = 0;
        var height = 1;
        var consensus = consensusService.Consensus;

        var vote = MakeRandomVote(signers[0], height, round, VoteType.PreVote);
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            _ = consensus.PreVoteAsync(vote, default);
        }, cancellationToken);

        await consensusService.StepChanged.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

        var i = 2;
        for (; i < 10; i++)
        {
            var waitTasks1 = blockchains.Select(item => item.TipChanged.WaitAsync(e => e.Height == i));
            await Task.WhenAll(waitTasks1);
            Array.ForEach(blockchains, item => Assert.Equal(i + 1, item.Blocks.Count));
            if (blockchains.Any(item => item.Blocks[i].Evidences.Count > 0))
            {
                break;
            }
        }

        Assert.NotEqual(10, i);

        var waitTasks2 = blockchains.Select(item => item.TipChanged.WaitAsync(e => e.Height == i));
        await Task.WhenAll(waitTasks2);
        foreach (Blockchain blockchain in blockchains)
        {
            Assert.Equal(i + 1, blockchain.Blocks.Count);
        }
    }

    private static Vote MakeRandomVote(
        ISigner signer, int height, int round, VoteType flag)
    {
        if (flag == VoteType.Null || flag == VoteType.Unknown)
        {
            throw new ArgumentException(
                $"{nameof(flag)} must be either {VoteType.PreVote} or {VoteType.PreCommit}" +
                $"to create a valid signed vote.");
        }

        var hash = new BlockHash(GetRandomBytes(BlockHash.Size));
        var voteMetadata = new VoteMetadata
        {
            Height = height,
            Round = round,
            BlockHash = hash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            ValidatorPower = BigInteger.One,
            Type = flag,
        };

        return voteMetadata.Sign(signer);

        static byte[] GetRandomBytes(int size)
        {
            var bytes = new byte[size];
            var random = new System.Random();
            random.NextBytes(bytes);

            return bytes;
        }
    }
}
