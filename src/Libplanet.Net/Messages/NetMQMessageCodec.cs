using Libplanet.Net.Transports;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;
using NetMQ;

namespace Libplanet.Net.Messages;

public class NetMQMessageCodec : IMessageCodec<NetMQMessage>
{
    public static readonly int CommonFrames =
        Enum.GetValues(typeof(MessageFrame)).Length;

    public NetMQMessageCodec()
    {
    }

    public enum MessageFrame
    {
        /// <summary>
        /// Frame containing <see cref="Protocol"/>.
        /// </summary>
        Version = 0,

        /// <summary>
        /// Frame containing the type of the message.
        /// </summary>
        Type = 1,

        /// <summary>
        /// Frame containing the sender <see cref="BoundPeer"/> of the<see cref="Message"/>.
        /// </summary>
        Peer = 2,

        /// <summary>
        /// Frame containing the datetime when the <see cref="Message"/> is created.
        /// </summary>
        Timestamp = 3,

        /// <summary>
        /// Frame containing signature of the <see cref="Message"/>.
        /// </summary>
        Sign = 4,
    }

    /// <inheritdoc cref="IMessageCodec{T}.Encode"/>
    public NetMQMessage Encode(
        Message message,
        PrivateKey privateKey)
    {
        if (!privateKey.PublicKey.Equals(message.Remote.PublicKey))
        {
            throw new InvalidCredentialException(
                $"An invalid private key was provided: the provided private key's " +
                $"expected public key is {message.Remote.PublicKey} " +
                $"but its actual public key is {privateKey.PublicKey}.",
                message.Remote.PublicKey,
                privateKey.PublicKey);
        }

        var netMqMessage = new NetMQMessage();

        // Write body (by concrete class)
        foreach (byte[] frame in message.Content.DataFrames)
        {
            netMqMessage.Append(frame);
        }

        // Write headers. (inverse order, version-type-peer-timestamp)
        netMqMessage.Push(message.Timestamp.Ticks);
        netMqMessage.Push(ModelSerializer.SerializeToBytes(message.Remote));
        netMqMessage.Push((int)message.Content.Type);
        netMqMessage.Push(message.Version.Token);

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

    /// <inheritdoc cref="IMessageCodec{T}.Decode"/>
    public Message Decode(NetMQMessage encoded, bool reply)
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
        var remote = ModelSerializer.DeserializeFromBytes<BoundPeer>(remains[(int)MessageFrame.Peer].ToByteArray());

        var type =
            (MessageContent.MessageType)remains[(int)MessageFrame.Type].ConvertToInt32();
        var ticks = remains[(int)MessageFrame.Timestamp].ConvertToInt64();
        var timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);

        byte[] signature = remains[(int)MessageFrame.Sign].ToByteArray();

        NetMQFrame[] body = remains.Skip(CommonFrames).ToArray();

        MessageContent content = CreateMessage(
            type,
            body.Select(frame => frame.ToByteArray()).ToArray());

        var headerWithoutSign = new[]
        {
            remains[(int)MessageFrame.Version],
            remains[(int)MessageFrame.Type],
            remains[(int)MessageFrame.Peer],
            remains[(int)MessageFrame.Timestamp],
        };

        var messageToVerify = headerWithoutSign.Concat(body).ToByteArray();
        if (!remote.PublicKey.Verify(messageToVerify, signature))
        {
            throw new InvalidMessageSignatureException(
                "The signature of an encoded message is invalid.",
                remote,
                remote.PublicKey,
                messageToVerify,
                signature);
        }

        byte[]? identity = reply ? null : encoded[0].Buffer.ToArray();

        return new Message(content, version, remote, timestamp, identity);
    }

    internal static MessageContent CreateMessage(
        MessageContent.MessageType type,
        byte[][] dataframes)
    {
        switch (type)
        {
            case MessageContent.MessageType.Ping:
                return new PingMessage();
            case MessageContent.MessageType.Pong:
                return new PongMessage();
            case MessageContent.MessageType.GetBlockHashes:
                return new GetBlockHashesMessage(dataframes);
            case MessageContent.MessageType.TxIds:
                return new TxIdsMessage(dataframes);
            case MessageContent.MessageType.EvidenceIds:
                return new EvidenceIdsMessage(dataframes);
            case MessageContent.MessageType.GetBlocks:
                return new GetBlocksMsg(dataframes);
            case MessageContent.MessageType.GetTxs:
                return new GetTransactionMessage(dataframes);
            case MessageContent.MessageType.GetEvidence:
                return new GetEvidenceMessage(dataframes);
            case MessageContent.MessageType.Blocks:
                return new BlocksMessage(dataframes);
            case MessageContent.MessageType.Tx:
                return new TransactionMessage(dataframes);
            case MessageContent.MessageType.Evidence:
                return new EvidenceMessage(dataframes);
            case MessageContent.MessageType.FindNeighbors:
                return new FindNeighborsMessage(dataframes);
            case MessageContent.MessageType.Neighbors:
                return new NeighborsMessage(dataframes);
            case MessageContent.MessageType.BlockHeaderMessage:
                return new BlockHeaderMessage(dataframes);
            case MessageContent.MessageType.BlockHashes:
                return new BlockHashesMessage(dataframes);
            case MessageContent.MessageType.GetChainStatus:
                return new GetChainStatusMessage();
            case MessageContent.MessageType.ChainStatus:
                return new ChainStatusMessage(dataframes);
            case MessageContent.MessageType.DifferentVersion:
                return new DifferentVersionMessage();
            case MessageContent.MessageType.HaveMessage:
                return new HaveMessage(dataframes);
            case MessageContent.MessageType.WantMessage:
                return new WantMessage(dataframes);
            case MessageContent.MessageType.ConsensusProposal:
                return new ConsensusProposalMessage(dataframes);
            case MessageContent.MessageType.ConsensusVote:
                return new ConsensusPreVoteMsg(dataframes);
            case MessageContent.MessageType.ConsensusCommit:
                return new ConsensusPreCommitMessage(dataframes);
            case MessageContent.MessageType.ConsensusMaj23Msg:
                return new ConsensusMaj23Msg(dataframes);
            case MessageContent.MessageType.ConsensusVoteSetBitsMsg:
                return new ConsensusVoteSetBitsMessage(dataframes);
            case MessageContent.MessageType.ConsensusProposalClaimMsg:
                return new ConsensusProposalClaimMessage(dataframes);
            default:
                throw new InvalidCastException($"Given type {type} is not a valid message.");
        }
    }
}
