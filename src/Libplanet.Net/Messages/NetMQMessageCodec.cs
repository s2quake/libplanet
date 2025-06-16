using Libplanet.Net.Transports;
using Libplanet.Serialization;
using Libplanet.Types;
using NetMQ;

namespace Libplanet.Net.Messages;

public sealed class NetMQMessageCodec
{
    public static readonly int CommonFrames = Enum.GetValues(typeof(MessageFrame)).Length;

    public NetMQMessageCodec()
    {
    }

    public enum MessageFrame
    {
        Version = 0,

        Type = 1,

        Peer = 2,

        Timestamp = 3,

        Sign = 4,
    }

    public NetMQMessage Encode(MessageEnvelope messageEnvelope, PrivateKey signer)
    {
        if (!signer.Address.Equals(messageEnvelope.Remote.Address))
        {
            throw new ArgumentException(
                $"The provided private key's address {signer.Address} does not match " +
                $"the remote peer's address {messageEnvelope.Remote.Address}.",
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

        // Write body (by concrete class)
        // netMqMessage.Append(ModelSerializer.SerializeToBytes(messageEnvelope.Protocol));
        // netMqMessage.Append(ModelSerializer.SerializeToBytes(messageEnvelope.Message));
        // netMqMessage.Push(ModelSerializer.SerializeToBytes(messageEnvelope.Remote));

        // Write headers. (inverse order, version-type-peer-timestamp)
        // netMqMessage.Push(messageEnvelope.Timestamp.Ticks);

        // Make and insert signature
        // var signature = signer.Sign(netMqMessage.ToByteArray());
        // var frames = netMqMessage.ToList();
        // frames.Insert((int)MessageFrame.Sign, new NetMQFrame(signature));
        // netMqMessage = new NetMQMessage(frames);
        // netMqMessage.Push(messageEnvelope.Id.ToByteArray());

        return netMqMessage;
    }

    public MessageEnvelope Decode(NetMQMessage encoded)
    {
        if (encoded.FrameCount is not 3)
        {
            throw new ArgumentException("Can't parse empty NetMQMessage.");
        }

        var bytes = encoded[1].ToByteArray();
        var signature = encoded[2].ToByteArray();

        var messageEnvelope = ModelSerializer.DeserializeFromBytes<MessageEnvelope>(bytes);
        var address = messageEnvelope.Remote.Address;
        if (!address.Verify(bytes, signature))
        {
            throw new InvalidOperationException("Signature verification failed.");
        }

        return messageEnvelope;


        // (reply == true)            [version, type, peer, timestamp, sign, frames...]
        // (reply == false) [identity, version, type, peer, timestamp, sign, frames...]
        // NetMQFrame[] remains = [.. encoded];

        // var versionToken = remains[(int)MessageFrame.Version].ToByteArray();

        // Protocol version = ModelSerializer.DeserializeFromBytes<Protocol>(versionToken);
        // var remote = ModelSerializer.DeserializeFromBytes<Peer>(remains[(int)MessageFrame.Peer].ToByteArray());

        // // var type =
        // //     (MessageBase.MessageType)remains[(int)MessageFrame.Type].ConvertToInt32();
        // var ticks = remains[(int)MessageFrame.Timestamp].ConvertToInt64();
        // var timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);

        // byte[] signature = remains[(int)MessageFrame.Sign].ToByteArray();

        // NetMQFrame[] body = remains.Skip(CommonFrames).ToArray();

        // IMessage content = CreateMessage(
        //     body[0].ToByteArray());

        // var headerWithoutSign = new[]
        // {
        //     remains[(int)MessageFrame.Version],
        //     remains[(int)MessageFrame.Type],
        //     remains[(int)MessageFrame.Peer],
        //     remains[(int)MessageFrame.Timestamp],
        // };

        // var messageToVerify = headerWithoutSign.Concat(body).ToByteArray();
        // if (!remote.Address.Verify(messageToVerify, signature))
        // {
        //     throw new InvalidOperationException("Signature verification failed.");
        // }

        // return new MessageEnvelope
        // {
        //     Message = content,
        //     Protocol = version,
        //     Remote = remote,
        //     Timestamp = timestamp,
        //     Id = new Guid(encoded[0].Buffer),
        // };
    }

    internal static IMessage CreateMessage(byte[] bytes)
    {
        return ModelSerializer.DeserializeFromBytes<IMessage>(bytes);
    }
}
