using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

public abstract record class MessageBase : IMessage
{
    [Property(0, InspectOnly = true)]
    public MessageId Id => new(SHA256.HashData(ModelSerializer.Serialize(this)));
}
