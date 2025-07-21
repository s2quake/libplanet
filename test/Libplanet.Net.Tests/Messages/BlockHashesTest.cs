using Libplanet.Net.Messages;
using Libplanet.TestUtilities;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Messages;

public sealed class BlockHashesTest(ITestOutputHelper output)
{
    [Fact]
    public void Decode()
    {
        var random = RandomUtility.GetRandom(output);
        var privateKey = RandomUtility.PrivateKey(random);
        var blockHashes = RandomUtility.Array(random, RandomUtility.BlockHash, 10);
        var peer = new Peer
        {
            Address = privateKey.Address,
            EndPoint = RandomUtility.DnsEndPoint(random),
        };
        var expected = new BlockHashMessage { BlockHashes = [.. blockHashes] };
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
        var actual = (BlockHashMessage)NetMQMessageCodec.Decode(rawMessage).Message;
        Assert.Equal(expected.BlockHashes, actual.BlockHashes.ToArray());
    }
}
