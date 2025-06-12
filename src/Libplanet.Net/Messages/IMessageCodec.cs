using Libplanet.Types;

namespace Libplanet.Net.Messages;

public interface IMessageCodec<T>
{
    T Encode(MessageEnvelope message, PrivateKey privateKey);

    MessageEnvelope Decode(T encoded, bool reply);
}
