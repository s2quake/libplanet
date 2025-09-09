using Libplanet.Net.Messages;
using Libplanet.Net.NetMQ;
using Libplanet.TestUtilities;

namespace Libplanet.Net.Tests.Messages;

public sealed class BlockHashesTest(ITestOutputHelper output)
{
    [Fact]
    public void Decode()
    {
        var random = Rand.GetRandom(output);
        var privateKey = Rand.PrivateKey(random);
        var blockHashes = Rand.Array(random, Rand.BlockHash, 10);
        var peer = new Peer
        {
            Address = privateKey.Address,
            EndPoint = Rand.DnsEndPoint(random),
        };
        var expected = new BlockHashResponseMessage { BlockHashes = [.. blockHashes] };
        Assert.Equal(blockHashes, expected.BlockHashes);
        var protocol = Protocol.Create(privateKey.AsSigner(), 3);
        var rawMessage = NetMQMessageCodec.Encode(
            new MessageEnvelope
            {
                Identity = Guid.NewGuid(),
                Message = expected,
                ProtocolHash = protocol.Hash,
                Sender = peer,
                Timestamp = DateTimeOffset.UtcNow,
            },
            privateKey.AsSigner());
        var actual = (BlockHashResponseMessage)NetMQMessageCodec.Decode(rawMessage).Message;
        Assert.Equal(expected.BlockHashes, actual.BlockHashes.ToArray());
    }
}
