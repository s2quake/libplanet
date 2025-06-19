using System.Net;
using Libplanet.Net.Messages;
using Libplanet.Net.Transports;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Types.Tests;
using NetMQ;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Net.Tests.Messages;

public class MessageTest
{
    [Fact]
    public void BlockHeaderMsg()
    {
        var privateKey = new PrivateKey();
        var peer = new Peer { Address = privateKey.Address, EndPoint = new DnsEndPoint("0.0.0.0", 0) };
        var apv = Protocol.Create(new PrivateKey(), 1);
        var dateTimeOffset = DateTimeOffset.UtcNow;
        Block genesis = ProposeGenesisBlock(GenesisProposer);
        var messageContent = new BlockHeaderMessage { GenesisHash = genesis.BlockHash, Excerpt = genesis };
        NetMQMessage raw = NetMQMessageCodec.Encode(
            new MessageEnvelope
            {
                Identity = Guid.NewGuid(),
                Message = messageContent,
                Protocol = apv,
                Peer = peer,
                Timestamp = dateTimeOffset,
            }, privateKey.AsSigner());
        var parsed = NetMQMessageCodec.Decode(raw);
        Assert.Equal(peer, parsed.Peer);
    }

    [Fact]
    public void InvalidCredential()
    {
        var ping = new PingMessage();
        var privateKey = new PrivateKey();
        var apv = Protocol.Create(new PrivateKey(), 1);
        var peer = new Peer { Address = privateKey.Address, EndPoint = new DnsEndPoint("0.0.0.0", 0) };
        var timestamp = DateTimeOffset.UtcNow;
        var badPrivateKey = new PrivateKey();
        Assert.Throws<InvalidOperationException>(() =>
            NetMQMessageCodec.Encode(
                new MessageEnvelope
                {
                    Identity = Guid.NewGuid(),
                    Message = ping,
                    Protocol = apv,
                    Peer = peer,
                    Timestamp = timestamp
                }, badPrivateKey.AsSigner()));
    }

    [Fact]
    public void UseInvalidSignature()
    {
        // Victim
        var privateKey = new PrivateKey();
        var peer = new Peer { Address = privateKey.Address, EndPoint = new DnsEndPoint("0.0.0.0", 0) };
        var timestamp = DateTimeOffset.UtcNow;
        var apv = Protocol.Create(new PrivateKey(), 1);
        var ping = new PingMessage();
        var netMqMessage = NetMQMessageCodec.Encode(
            new MessageEnvelope
            {
                Identity = Guid.NewGuid(),
                Message = ping,
                Protocol = apv,
                Peer = peer,
                Timestamp = timestamp
            }, privateKey.AsSigner()).ToArray();

        // Attacker
        var fakePeer = new Peer { Address = privateKey.Address, EndPoint = new DnsEndPoint("1.2.3.4", 0) };
        var fakeMessage = NetMQMessageCodec.Encode(
            new MessageEnvelope
            {
                Identity = Guid.NewGuid(),
                Message = ping,
                Protocol = apv,
                Peer = fakePeer,
                Timestamp = timestamp,
            }, privateKey.AsSigner()).ToArray();

        var frames = new NetMQMessage();
        frames.Push(netMqMessage[4]);
        frames.Push(netMqMessage[3]);
        frames.Push(fakeMessage[2]);
        frames.Push(netMqMessage[1]);
        frames.Push(netMqMessage[0]);

        Assert.Throws<InvalidOperationException>(() =>
            NetMQMessageCodec.Decode(frames));
    }

    [Fact]
    public void InvalidArguments()
    {
        var message = new PingMessage();
        var privateKey = new PrivateKey();
        var apv = Protocol.Create(new PrivateKey(), 1);
        Assert.Throws<ArgumentException>(
            () => NetMQMessageCodec.Decode(new NetMQMessage()));
    }

    [Fact]
    public void GetId()
    {
        var privateKey = new PrivateKey();
        var peer = new Peer { Address = privateKey.Address, EndPoint = new DnsEndPoint("1.2.3.4", 1234) };
        var apv = Protocol.Create(new PrivateKey(), 1);
        var dateTimeOffset = DateTimeOffset.MinValue + TimeSpan.FromHours(6.1234);
        Block genesis = ProposeGenesisBlock(GenesisProposer);
        var message = new BlockHeaderMessage { GenesisHash = genesis.BlockHash, Excerpt = genesis };
        Assert.Equal(
            new MessageId(ByteUtility.ParseHex(
                "e1acbdc4d0cc1eb156cec60d0bf6d40fae3a90192e95719b12e6ee944c71b742")),
            message.Id);
    }

    [Fact]
    public void InvalidVoteFlagConsensus()
    {
        var blockHash = new BlockHash(RandomUtility.Bytes(BlockHash.Size));

        var preVote = TestUtils.CreateVote(
            TestUtils.PrivateKeys[0],
            TestUtils.Validators[0].Power,
            1,
            0,
            blockHash,
            VoteFlag.PreVote);

        var preCommit = TestUtils.CreateVote(
            TestUtils.PrivateKeys[0],
            TestUtils.Validators[0].Power,
            1,
            0,
            blockHash,
            VoteFlag.PreCommit);

        // Valid message cases
        _ = new ConsensusPreVoteMessage { PreVote = preVote };
        _ = new ConsensusPreCommitMessage { PreCommit = preCommit };

        // Invalid message cases
        Assert.Throws<ArgumentException>(() => new ConsensusPreVoteMessage { PreVote = preCommit });
        Assert.Throws<ArgumentException>(() => new ConsensusPreCommitMessage { PreCommit = preVote });
    }
}
