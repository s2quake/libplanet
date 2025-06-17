using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

public abstract record class MessageBase : IMessage
{
    public MessageId Id => new(SHA256.HashData(ModelSerializer.SerializeToBytes(this)));
}
