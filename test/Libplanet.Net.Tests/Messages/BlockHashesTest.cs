using System.Net;
using Libplanet.Net.Messages;
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
            var messageContent = new BlockHashesMessage { Hashes = [.. blockHashes] };
            Assert.Equal(blockHashes, messageContent.Hashes);
            var privateKey = new PrivateKey();
            Protocol apv = Protocol.Create(privateKey, 3);
            var peer = new Peer { Address = privateKey.Address, EndPoint = new DnsEndPoint("0.0.0.0", 1234) };
            var messageCodec = new NetMQMessageCodec();
            NetMQMessage encoded = messageCodec.Encode(
                new MessageEnvelope
                {
                    Id = Guid.NewGuid(),
                    Message = messageContent,
                    Protocol = apv,
                    Remote = peer,
                    Timestamp = DateTimeOffset.UtcNow,
                },
                privateKey);
            BlockHashesMessage restored = (BlockHashesMessage)messageCodec.Decode(encoded).Message;
            Assert.Equal(messageContent.Hashes, restored.Hashes);
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
