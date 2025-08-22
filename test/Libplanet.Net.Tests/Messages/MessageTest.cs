using Libplanet.Net.Messages;
using Libplanet.Net.NetMQ;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
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
        var signer = RandomUtility.Signer(random);
        var peer = new Peer
        {
            Address = signer.Address,
            EndPoint = RandomUtility.DnsEndPoint(random),
        };
        var protocol = new ProtocolBuilder { Version = 1 }.Create(RandomUtility.Signer(random));
        var dateTimeOffset = DateTimeOffset.UtcNow;
        var genesis = ProposeGenesisBlock(GenesisProposer);
        var expected = new BlockSummaryMessage { GenesisBlockHash = genesis.BlockHash, BlockSummary = genesis };
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
        Assert.Throws<InvalidOperationException>(() =>
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
        var genesis = ProposeGenesisBlock(GenesisProposer);
        var message = new BlockSummaryMessage { GenesisBlockHash = genesis.BlockHash, BlockSummary = genesis };
        Assert.Equal(
            new MessageId(ByteUtility.ParseHex(
                "e1acbdc4d0cc1eb156cec60d0bf6d40fae3a90192e95719b12e6ee944c71b742")),
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
        ValidationUtility.Validate(() => new ConsensusPreVoteMessage { PreVote = preVote });
        ValidationUtility.Validate(() => new ConsensusPreCommitMessage { PreCommit = preCommit });

        // Invalid message cases
        ValidationTest.Throws(() => new ConsensusPreVoteMessage { PreVote = preCommit });
        ValidationTest.Throws(() => new ConsensusPreCommitMessage { PreCommit = preVote });
    }
}
