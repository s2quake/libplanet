using System.Net;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Consensus;
using Libplanet.Crypto;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using NetMQ;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Net.Tests.Messages
{
    [Collection("NetMQConfiguration")]
    public class NetMQMessageCodecTest : IDisposable
    {
        public void Dispose()
        {
            NetMQConfig.Cleanup(false);
        }

        [Theory]
        [InlineData(MessageContent.MessageType.Ping)]
        [InlineData(MessageContent.MessageType.Pong)]
        [InlineData(MessageContent.MessageType.GetBlockHashes)]
        [InlineData(MessageContent.MessageType.TxIds)]
        [InlineData(MessageContent.MessageType.GetBlocks)]
        [InlineData(MessageContent.MessageType.GetTxs)]
        [InlineData(MessageContent.MessageType.Blocks)]
        [InlineData(MessageContent.MessageType.Tx)]
        [InlineData(MessageContent.MessageType.FindNeighbors)]
        [InlineData(MessageContent.MessageType.Neighbors)]
        [InlineData(MessageContent.MessageType.BlockHeaderMessage)]
        [InlineData(MessageContent.MessageType.BlockHashes)]
        [InlineData(MessageContent.MessageType.GetChainStatus)]
        [InlineData(MessageContent.MessageType.ChainStatus)]
        [InlineData(MessageContent.MessageType.DifferentVersion)]
        [InlineData(MessageContent.MessageType.HaveMessage)]
        [InlineData(MessageContent.MessageType.WantMessage)]
        [InlineData(MessageContent.MessageType.ConsensusProposal)]
        [InlineData(MessageContent.MessageType.ConsensusVote)]
        [InlineData(MessageContent.MessageType.ConsensusCommit)]
        [InlineData(MessageContent.MessageType.ConsensusMaj23Msg)]
        [InlineData(MessageContent.MessageType.ConsensusVoteSetBitsMsg)]
        [InlineData(MessageContent.MessageType.ConsensusProposalClaimMsg)]
        public void CheckMessages(MessageContent.MessageType type)
        {
            var privateKey = new PrivateKey();
            var peer = new BoundPeer(privateKey.PublicKey, new DnsEndPoint("0.0.0.0", 0));
            var dateTimeOffset = DateTimeOffset.UtcNow;
            var apv = new AppProtocolVersion(
                1,
                new Bencodex.Types.Integer(0),
                ImmutableArray<byte>.Empty,
                default(Address));
            var messageContent = CreateMessage(type);
            var codec = new NetMQMessageCodec();
            NetMQMessage raw =
                codec.Encode(
                    new Message(messageContent, apv, peer, dateTimeOffset, null),
                    privateKey);
            var parsed = codec.Decode(raw, true);
            Assert.Equal(apv, parsed.Version);
            Assert.Equal(peer, parsed.Remote);
            Assert.Equal(dateTimeOffset, parsed.Timestamp);
            Assert.IsType(messageContent.GetType(), parsed.Content);
            Assert.Equal(messageContent.DataFrames, parsed.Content.DataFrames);
        }

        private MessageContent CreateMessage(MessageContent.MessageType type)
        {
            var privateKey = new PrivateKey();
            var boundPeer = new BoundPeer(privateKey.PublicKey, new DnsEndPoint("127.0.0.1", 1000));
            IBlockPolicy policy = new BlockPolicy();
            BlockChain chain = MakeBlockChain(
                policy,
                new MemoryStore(),
                new TrieStateStore());
            var codec = new Codec();
            Block genesis = chain.Genesis;
            var transaction = chain.MakeTransaction(privateKey, new DumbAction[] { });
            switch (type)
            {
                case MessageContent.MessageType.Ping:
                    return new PingMsg();
                case MessageContent.MessageType.Pong:
                    return new PongMsg();
                case MessageContent.MessageType.GetBlockHashes:
                    return new GetBlockHashesMsg(chain.GetBlockLocator());
                case MessageContent.MessageType.TxIds:
                    return new TxIdsMsg(new[] { transaction.Id });
                case MessageContent.MessageType.GetBlocks:
                    return new GetBlocksMsg(new[] { genesis.Hash }, 10);
                case MessageContent.MessageType.GetTxs:
                    return new GetTxsMsg(new[] { transaction.Id });
                case MessageContent.MessageType.Blocks:
                    return new Libplanet.Net.Messages.BlocksMsg(new[]
                    {
                        BitConverter.GetBytes(2),
                        ModelSerializer.SerializeToBytes(genesis),
                        Array.Empty<byte>(),
                    });
                case MessageContent.MessageType.Tx:
                    return new Libplanet.Net.Messages.TxMsg(transaction.Serialize());
                case MessageContent.MessageType.FindNeighbors:
                    return new FindNeighborsMsg(privateKey.Address);
                case MessageContent.MessageType.Neighbors:
                    return new NeighborsMsg(new[] { boundPeer });
                case MessageContent.MessageType.BlockHeaderMessage:
                    return new BlockHeaderMsg(genesis.Hash, genesis.Header);
                case MessageContent.MessageType.BlockHashes:
                    return new BlockHashesMsg(new[] { genesis.Hash });
                case MessageContent.MessageType.GetChainStatus:
                    return new GetChainStatusMsg();
                case MessageContent.MessageType.ChainStatus:
                    return new ChainStatusMsg(
                        0,
                        genesis.Hash,
                        chain.Tip.Height,
                        chain.Tip.Hash);
                case MessageContent.MessageType.DifferentVersion:
                    return new DifferentVersionMsg();
                case MessageContent.MessageType.HaveMessage:
                    return new HaveMessage(
                        new[] { new MessageId(TestUtils.GetRandomBytes(MessageId.Size)) });
                case MessageContent.MessageType.WantMessage:
                    return new WantMessage(
                        new[] { new MessageId(TestUtils.GetRandomBytes(MessageId.Size)) });
                case MessageContent.MessageType.ConsensusProposal:
                    return new ConsensusProposalMsg(
                        new ProposalMetadata(
                            0,
                            0,
                            DateTimeOffset.UtcNow,
                            privateKey.PublicKey,
                            ModelSerializer.SerializeToBytes(genesis),
                            -1).Sign(privateKey));
                case MessageContent.MessageType.ConsensusVote:
                    return new ConsensusPreVoteMsg(
                            new VoteMetadata
                            {
                                Height = 0,
                                Round = 0,
                                BlockHash = genesis.Hash,
                                Timestamp = DateTimeOffset.UtcNow,
                                ValidatorPublicKey = privateKey.PublicKey,
                                ValidatorPower = BigInteger.One,
                                Flag = VoteFlag.PreVote,
                            }.Sign(privateKey));
                case MessageContent.MessageType.ConsensusCommit:
                    return new ConsensusPreCommitMsg(
                        new VoteMetadata
                        {
                            Height = 0,
                            Round = 0,
                            BlockHash = genesis.Hash,
                            Timestamp = DateTimeOffset.UtcNow,
                            ValidatorPublicKey = privateKey.PublicKey,
                            ValidatorPower = BigInteger.One,
                            Flag = VoteFlag.PreCommit,
                        }.Sign(privateKey));
                case MessageContent.MessageType.ConsensusMaj23Msg:
                    return new ConsensusMaj23Msg(
                        new Maj23Metadata(
                            0,
                            0,
                            genesis.Hash,
                            DateTimeOffset.UtcNow,
                            privateKey.PublicKey,
                            VoteFlag.PreVote).Sign(privateKey));
                case MessageContent.MessageType.ConsensusVoteSetBitsMsg:
                    return new ConsensusVoteSetBitsMsg(
                        new VoteSetBitsMetadata(
                            0,
                            0,
                            genesis.Hash,
                            DateTimeOffset.UtcNow,
                            privateKey.PublicKey,
                            VoteFlag.PreVote,
                            new[] { true, true, false, false }).Sign(privateKey));
                case MessageContent.MessageType.ConsensusProposalClaimMsg:
                    return new ConsensusProposalClaimMsg(
                        new ProposalClaimMetadata(
                            0,
                            0,
                            genesis.Hash,
                            DateTimeOffset.UtcNow,
                            privateKey.PublicKey).Sign(privateKey));
                default:
                    throw new Exception($"Cannot create a message of invalid type {type}");
            }
        }
    }
}
