using Libplanet.Net.Transports;
using Libplanet.Serialization;
using Libplanet.Types;
using NetMQ;

namespace Libplanet.Net.Messages;

public sealed class NetMQMessageCodec : IMessageCodec<NetMQMessage>
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

    public NetMQMessage Encode(MessageEnvelope message, PrivateKey privateKey)
    {
        if (!privateKey.Address.Equals(message.Remote.Address))
        {
            throw new InvalidCredentialException(
                $"An invalid private key was provided: the provided private key's " +
                $"expected public key is {message.Remote.Address} " +
                $"but its actual public key is {privateKey.Address}.",
                message.Remote.Address,
                privateKey.Address);
        }

        var netMqMessage = new NetMQMessage();

        // Write body (by concrete class)
        netMqMessage.Append(ModelSerializer.SerializeToBytes(message.Message));

        // Write headers. (inverse order, version-type-peer-timestamp)
        netMqMessage.Push(message.Timestamp.Ticks);
        netMqMessage.Push(ModelSerializer.SerializeToBytes(message.Remote));
        netMqMessage.Push(message.Protocol.Token);

        // Make and insert signature
        byte[] signature = privateKey.Sign(netMqMessage.ToByteArray());
        List<NetMQFrame> frames = netMqMessage.ToList();
        frames.Insert((int)MessageFrame.Sign, new NetMQFrame(signature));
        netMqMessage = new NetMQMessage(frames);

        if (message.Identity is { })
        {
            netMqMessage.Push(message.Identity);
        }

        return netMqMessage;
    }

    public MessageEnvelope Decode(NetMQMessage encoded, bool reply)
    {
        if (encoded.FrameCount == 0)
        {
            throw new ArgumentException("Can't parse empty NetMQMessage.");
        }

        // (reply == true)            [version, type, peer, timestamp, sign, frames...]
        // (reply == false) [identity, version, type, peer, timestamp, sign, frames...]
        NetMQFrame[] remains = reply ? encoded.ToArray() : encoded.Skip(1).ToArray();

        var versionToken = remains[(int)MessageFrame.Version].ConvertToString();

        Protocol version = Protocol.FromToken(versionToken);
        var remote = ModelSerializer.DeserializeFromBytes<Peer>(remains[(int)MessageFrame.Peer].ToByteArray());

        // var type =
        //     (MessageBase.MessageType)remains[(int)MessageFrame.Type].ConvertToInt32();
        var ticks = remains[(int)MessageFrame.Timestamp].ConvertToInt64();
        var timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);

        byte[] signature = remains[(int)MessageFrame.Sign].ToByteArray();

        NetMQFrame[] body = remains.Skip(CommonFrames).ToArray();

        IMessage content = CreateMessage(
            body[0].ToByteArray());

        var headerWithoutSign = new[]
        {
            remains[(int)MessageFrame.Version],
            remains[(int)MessageFrame.Type],
            remains[(int)MessageFrame.Peer],
            remains[(int)MessageFrame.Timestamp],
        };

        var messageToVerify = headerWithoutSign.Concat(body).ToByteArray();
        if (!remote.Address.Verify(messageToVerify, signature))
        {
            throw new InvalidMessageSignatureException(
                "The signature of an encoded message is invalid.",
                remote,
                remote.Address,
                messageToVerify,
                signature);
        }

        byte[] identity = reply ? [] : encoded[0].Buffer.ToArray();

        return new MessageEnvelope
        {
            Message = content,
            Protocol = version,
            Remote = remote,
            Timestamp = timestamp,
            Identity = identity,
        };
    }

    internal static IMessage CreateMessage(byte[] bytes)
    {
        return ModelSerializer.DeserializeFromBytes<IMessage>(bytes);
    }
}
