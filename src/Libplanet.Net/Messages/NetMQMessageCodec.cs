using Libplanet.Serialization;
using Libplanet.Types;
using NetMQ;

namespace Libplanet.Net.Messages;

internal static class NetMQMessageCodec
{
    public static NetMQMessage Encode(MessageEnvelope messageEnvelope, ISigner signer)
    {
        if (!signer.Address.Equals(messageEnvelope.Sender.Address))
        {
            throw new ArgumentException(
                $"The provided private key's address {signer.Address} does not match " +
                $"the remote peer's address {messageEnvelope.Sender.Address}.",
                nameof(signer));
        }

        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var bytes = ModelSerializer.SerializeToBytes(messageEnvelope, options);
        var signature = signer.Sign(bytes);
        var netMqMessage = new NetMQMessage();
        netMqMessage.Append(bytes);
        netMqMessage.Append(signature);

        return netMqMessage;
    }

    public static MessageEnvelope Decode(NetMQMessage encoded)
    {
        if (encoded.FrameCount < 2)
        {
            throw new ArgumentException("Invalid NetMQMessage: must contain at least 2 frames.");
        }

        var bytes = encoded[0].ToByteArray();
        var signature = encoded[1].ToByteArray();

        var messageEnvelope = ModelSerializer.DeserializeFromBytes<MessageEnvelope>(bytes);
        var address = messageEnvelope.Sender.Address;
        if (!address.Verify(bytes, signature))
        {
            throw new InvalidOperationException("Signature verification failed.");
        }

        return messageEnvelope;
    }
}
