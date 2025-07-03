using System.Net;
using Libplanet.Net.Messages;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using NetMQ;

namespace Libplanet.Net.Tests.Messages
{
    [Collection("NetMQConfiguration")]
    public class BlockHashesTest : IDisposable
    {
        public void Dispose()
        {
            NetMQConfig.Cleanup(false);
        }

        [Fact]
        public void Decode()
        {
            BlockHash[] blockHashes = GenerateRandomBlockHashes(100L).ToArray();
            var messageContent = new BlockHashMessage { BlockHashes = [.. blockHashes] };
            Assert.Equal(blockHashes, messageContent.BlockHashes);
            var privateKey = new PrivateKey();
            Protocol apv = Protocol.Create(privateKey, 3);
            var peer = new Peer { Address = privateKey.Address, EndPoint = new DnsEndPoint("0.0.0.0", 1234) };
            NetMQMessage encoded = NetMQMessageCodec.Encode(
                new MessageEnvelope
                {
                    Identity = Guid.NewGuid(),
                    Message = messageContent,
                    Protocol = apv,
                    Peer = peer,
                    Timestamp = DateTimeOffset.UtcNow,
                },
                privateKey.AsSigner());
            BlockHashMessage restored = (BlockHashMessage)NetMQMessageCodec.Decode(encoded).Message;
            Assert.Equal(messageContent.BlockHashes, restored.BlockHashes);
        }

        private static IEnumerable<BlockHash> GenerateRandomBlockHashes(long count)
        {
            var random = new Random();
            var buffer = new byte[32];
            for (long i = 0; i < count; i++)
            {
                random.NextBytes(buffer);
                yield return new BlockHash(buffer);
            }
        }
    }
}
