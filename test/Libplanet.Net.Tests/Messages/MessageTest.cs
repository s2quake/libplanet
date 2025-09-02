using Libplanet.Net.Messages;
using Libplanet.Net.NetMQ;
using Libplanet.Serialization;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.Types;
using NetMQ;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Net.Tests.Messages;

public sealed class MessageTest(ITestOutputHelper output)
{
    [Fact]
    public void BlockHeaderMsg()
    {
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var signer = RandomUtility.Signer(random);
        var peer = new Peer
        {
            Address = signer.Address,
            EndPoint = RandomUtility.DnsEndPoint(random),
        };
        var protocol = new ProtocolBuilder { Version = 1 }.Create(RandomUtility.Signer(random));
        var dateTimeOffset = DateTimeOffset.UtcNow;
        var expected = new BlockSummaryMessage
        {
            GenesisBlockHash = genesisBlock.BlockHash,
            BlockSummary = genesisBlock,
        };
        var rawMessage = NetMQMessageCodec.Encode(
            new MessageEnvelope
            {
                Identity = Guid.NewGuid(),
                Message = expected,
                ProtocolHash = protocol.Hash,
                Sender = peer,
                Timestamp = dateTimeOffset,
            }, signer);
        var actual = NetMQMessageCodec.Decode(rawMessage);
        Assert.Equal(peer, actual.Sender);
    }

    [Fact]
    public void InvalidCredential()
    {
        var random = RandomUtility.GetRandom(output);
        var message = new PingMessage();
        var signer = RandomUtility.Signer(random);
        var protocol = new ProtocolBuilder { Version = 1 }.Create(RandomUtility.Signer(random));
        var peer = new Peer
        {
            Address = signer.Address,
            EndPoint = RandomUtility.DnsEndPoint(random),
        };
        var timestamp = DateTimeOffset.UtcNow;
        var badPrivateKey = new PrivateKey();
        Assert.Throws<ArgumentException>(() =>
            NetMQMessageCodec.Encode(
                new MessageEnvelope
                {
                    Identity = Guid.NewGuid(),
                    Message = message,
                    ProtocolHash = protocol.Hash,
                    Sender = peer,
                    Timestamp = timestamp
                }, badPrivateKey.AsSigner()));
    }

    [Fact]
    public void UseInvalidSignature()
    {
        var random = RandomUtility.GetRandom(output);
        // Victim
        var signer = RandomUtility.Signer(random);
        var peer = new Peer
        {
            Address = signer.Address,
            EndPoint = RandomUtility.DnsEndPoint(random),
        };
        var timestamp = DateTimeOffset.UtcNow;
        var protocol = new ProtocolBuilder { Version = 1 }.Create(RandomUtility.Signer(random));
        var ping = new PingMessage();
        var rawMessage = NetMQMessageCodec.Encode(
            new MessageEnvelope
            {
                Identity = Guid.NewGuid(),
                Message = ping,
                ProtocolHash = protocol.Hash,
                Sender = peer,
                Timestamp = timestamp
            }, signer).ToArray();

        // Attacker
        var fakePeer = new Peer
        {
            Address = signer.Address,
            EndPoint = RandomUtility.DnsEndPoint(random),
        };
        var fakeMessage = NetMQMessageCodec.Encode(
            new MessageEnvelope
            {
                Identity = Guid.NewGuid(),
                Message = ping,
                ProtocolHash = protocol.Hash,
                Sender = fakePeer,
                Timestamp = timestamp,
            }, signer).ToArray();

        var frames = new NetMQMessage();
        frames.Push(rawMessage[4]);
        frames.Push(rawMessage[3]);
        frames.Push(fakeMessage[2]);
        frames.Push(rawMessage[1]);
        frames.Push(rawMessage[0]);

        Assert.Throws<InvalidOperationException>(() => NetMQMessageCodec.Decode(frames));
    }

    [Fact]
    public void InvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => NetMQMessageCodec.Decode(new NetMQMessage()));
    }

    [Fact]
    public void GetId()
    {
        var random = RandomUtility.GetStaticRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
            Timestamp = new DateTimeOffset(2025, 9, 2, 14, 56, 15, TimeSpan.Zero),
        }.Create(proposer);
        var message = new BlockSummaryMessage
        {
            GenesisBlockHash = genesisBlock.BlockHash,
            BlockSummary = genesisBlock,
        };
        Assert.Equal(
            MessageId.Parse("905384adec66e8c79103c7a3f0e544d2cecc846b39419559a9603e5172f21130"),
            message.Id);
    }

    [Fact]
    public void InvalidVoteTypeConsensus()
    {
        var blockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));

        var preVote = new VoteMetadata
        {
            Validator = TestUtils.Validators[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Height = 1,
            BlockHash = blockHash,
            Type = VoteType.PreVote,
        }.Sign(TestUtils.Signers[0]);

        var preCommit = new VoteMetadata
        {
            Validator = TestUtils.Validators[0].Address,
            ValidatorPower = TestUtils.Validators[0].Power,
            Height = 1,
            BlockHash = blockHash,
            Type = VoteType.PreCommit,
        }.Sign(TestUtils.Signers[0]);

        // Valid message cases
        ModelValidationUtility.Validate(() => new ConsensusPreVoteMessage { PreVote = preVote });
        ModelValidationUtility.Validate(() => new ConsensusPreCommitMessage { PreCommit = preCommit });

        // Invalid message cases
        ModelAssert.Throws(() => new ConsensusPreVoteMessage { PreVote = preCommit });
        ModelAssert.Throws(() => new ConsensusPreCommitMessage { PreCommit = preVote });
    }
}
