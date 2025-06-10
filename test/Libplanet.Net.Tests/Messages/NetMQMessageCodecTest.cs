// using System.Net;
// using Libplanet;
// using Libplanet.Net.Consensus;
// using Libplanet.Net.Messages;
// using Libplanet.Serialization;
// using Libplanet.Types;
// using Libplanet.Types;
// using Libplanet.Types;
// using NetMQ;
// using static Libplanet.Tests.TestUtils;

// namespace Libplanet.Net.Tests.Messages
// {
//     [Collection("NetMQConfiguration")]
//     public class NetMQMessageCodecTest : IDisposable
//     {
//         public void Dispose()
//         {
//             NetMQConfig.Cleanup(false);
//         }

//         [Theory]
//         [InlineData(MessageContent.MessageType.Ping)]
//         [InlineData(MessageContent.MessageType.Pong)]
//         [InlineData(MessageContent.MessageType.GetBlockHashes)]
//         [InlineData(MessageContent.MessageType.TxIds)]
//         [InlineData(MessageContent.MessageType.GetBlocks)]
//         [InlineData(MessageContent.MessageType.GetTxs)]
//         [InlineData(MessageContent.MessageType.Blocks)]
//         [InlineData(MessageContent.MessageType.Tx)]
//         [InlineData(MessageContent.MessageType.FindNeighbors)]
//         [InlineData(MessageContent.MessageType.Neighbors)]
//         [InlineData(MessageContent.MessageType.BlockHeaderMessage)]
//         [InlineData(MessageContent.MessageType.BlockHashes)]
//         [InlineData(MessageContent.MessageType.GetChainStatus)]
//         [InlineData(MessageContent.MessageType.ChainStatus)]
//         [InlineData(MessageContent.MessageType.DifferentVersion)]
//         [InlineData(MessageContent.MessageType.HaveMessage)]
//         [InlineData(MessageContent.MessageType.WantMessage)]
//         [InlineData(MessageContent.MessageType.ConsensusProposal)]
//         [InlineData(MessageContent.MessageType.ConsensusVote)]
//         [InlineData(MessageContent.MessageType.ConsensusCommit)]
//         [InlineData(MessageContent.MessageType.ConsensusMaj23Msg)]
//         [InlineData(MessageContent.MessageType.ConsensusVoteSetBitsMsg)]
//         [InlineData(MessageContent.MessageType.ConsensusProposalClaimMsg)]
//         public void CheckMessages(MessageContent.MessageType type)
//         {
//             var privateKey = new PrivateKey();
//             var peer = new BoundPeer(privateKey.PublicKey, new DnsEndPoint("0.0.0.0", 0));
//             var dateTimeOffset = DateTimeOffset.UtcNow;
//             var apv = new Protocol(
//                 1,
//                 ModelSerializer.SerializeToBytes(0),
//                 ImmutableArray<byte>.Empty,
//                 default);
//             var messageContent = CreateMessage(type);
//             var codec = new NetMQMessageCodec();
//             NetMQMessage raw =
//                 codec.Encode(
//                     new Message(messageContent, apv, peer, dateTimeOffset, null),
//                     privateKey);
//             var parsed = codec.Decode(raw, true);
//             Assert.Equal(apv, parsed.Version);
//             Assert.Equal(peer, parsed.Remote);
//             Assert.Equal(dateTimeOffset, parsed.Timestamp);
//             Assert.IsType(messageContent.GetType(), parsed.Content);
//             Assert.Equal(messageContent.DataFrames, parsed.Content.DataFrames);
//         }

//         private MessageContent CreateMessage(MessageContent.MessageType type)
//         {
//             var privateKey = new PrivateKey();
//             var boundPeer = new BoundPeer(privateKey.PublicKey, new DnsEndPoint("127.0.0.1", 1000));
//             Blockchain chain = MakeBlockChain();
//             Block genesis = chain.Genesis;
//             var transaction = chain.StagedTransactions.Add(new TransactionSubmission
//             {
//                 Signer = privateKey,
//             });
//             switch (type)
//             {
//                 case MessageContent.MessageType.Ping:
//                     return new PingMessage();
//                 case MessageContent.MessageType.Pong:
//                     return new PongMessage();
//                 case MessageContent.MessageType.GetBlockHashes:
//                     return new GetBlockHashesMessage(chain.Tip.BlockHash);
//                 case MessageContent.MessageType.TxIds:
//                     return new TxIdsMessage(new[] { transaction.Id });
//                 case MessageContent.MessageType.GetBlocks:
//                     return new GetBlocksMsg(new[] { genesis.BlockHash }, 10);
//                 case MessageContent.MessageType.GetTxs:
//                     return new GetTransactionMessage(new[] { transaction.Id });
//                 case MessageContent.MessageType.Blocks:
//                     return new Libplanet.Net.Messages.BlocksMessage(new[]
//                     {
//                         BitConverter.GetBytes(2),
//                         ModelSerializer.SerializeToBytes(genesis),
//                         Array.Empty<byte>(),
//                     });
//                 case MessageContent.MessageType.Tx:
//                     return new Libplanet.Net.Messages.TransactionMessage(ModelSerializer.SerializeToBytes(transaction));
//                 case MessageContent.MessageType.FindNeighbors:
//                     return new FindNeighborsMessage(privateKey.Address);
//                 case MessageContent.MessageType.Neighbors:
//                     return new NeighborsMessage(new[] { boundPeer });
//                 case MessageContent.MessageType.BlockHeaderMessage:
//                     return new BlockHeaderMessage(genesis.BlockHash, genesis);
//                 case MessageContent.MessageType.BlockHashes:
//                     return new BlockHashesMessage(new[] { genesis.BlockHash });
//                 case MessageContent.MessageType.GetChainStatus:
//                     return new GetChainStatusMessage();
//                 case MessageContent.MessageType.ChainStatus:
//                     return new ChainStatusMessage(
//                         0,
//                         genesis.BlockHash,
//                         chain.Tip.Height,
//                         chain.Tip.BlockHash);
//                 case MessageContent.MessageType.DifferentVersion:
//                     return new DifferentVersionMessage();
//                 case MessageContent.MessageType.HaveMessage:
//                     return new HaveMessage(
//                         new[] { new MessageId(RandomUtility.Bytes(MessageId.Size)) });
//                 case MessageContent.MessageType.WantMessage:
//                     return new WantMessage(
//                         new[] { new MessageId(RandomUtility.Bytes(MessageId.Size)) });
//                 case MessageContent.MessageType.ConsensusProposal:
//                     return new ConsensusProposalMessage(
//                         new ProposalMetadata
//                         {
//                             Height = 0,
//                             Round = 0,
//                             Timestamp = DateTimeOffset.UtcNow,
//                             Proposer = privateKey.Address,
//                             // MarshaledBlock = ModelSerializer.SerializeToBytes(genesis),
//                             ValidRound = -1,
//                         }.Sign(privateKey));
//                 case MessageContent.MessageType.ConsensusVote:
//                     return new ConsensusPreVoteMsg(
//                             new VoteMetadata
//                             {
//                                 Height = 0,
//                                 Round = 0,
//                                 BlockHash = genesis.BlockHash,
//                                 Timestamp = DateTimeOffset.UtcNow,
//                                 Validator = privateKey.Address,
//                                 ValidatorPower = BigInteger.One,
//                                 Flag = VoteFlag.PreVote,
//                             }.Sign(privateKey));
//                 case MessageContent.MessageType.ConsensusCommit:
//                     return new ConsensusPreCommitMessage(
//                         new VoteMetadata
//                         {
//                             Height = 0,
//                             Round = 0,
//                             BlockHash = genesis.BlockHash,
//                             Timestamp = DateTimeOffset.UtcNow,
//                             Validator = privateKey.Address,
//                             ValidatorPower = BigInteger.One,
//                             Flag = VoteFlag.PreCommit,
//                         }.Sign(privateKey));
//                 case MessageContent.MessageType.ConsensusMaj23Msg:
//                     return new ConsensusMaj23Msg(
//                         new Maj23Metadata
//                         {
//                             Height = 0,
//                             Round = 0,
//                             BlockHash = genesis.BlockHash,
//                             Timestamp = DateTimeOffset.UtcNow,
//                             Validator = privateKey.Address,
//                             Flag = VoteFlag.PreVote,
//                         }.Sign(privateKey));
//                 case MessageContent.MessageType.ConsensusVoteSetBitsMsg:
//                     return new ConsensusVoteSetBitsMessage(
//                         new VoteSetBitsMetadata
//                         {
//                             Height = 0,
//                             Round = 0,
//                             BlockHash = genesis.BlockHash,
//                             Timestamp = DateTimeOffset.UtcNow,
//                             Validator = privateKey.Address,
//                             Flag = VoteFlag.PreVote,
//                             VoteBits = [true, true, false, false],
//                         }.Sign(privateKey));
//                 case MessageContent.MessageType.ConsensusProposalClaimMsg:
//                     return new ConsensusProposalClaimMessage(
//                         new ProposalClaimMetadata
//                         {
//                             Height = 0,
//                             Round = 0,
//                             BlockHash = genesis.BlockHash,
//                             Timestamp = DateTimeOffset.UtcNow,
//                             Validator = privateKey.Address,
//                         }.Sign(privateKey));
//                 default:
//                     throw new Exception($"Cannot create a message of invalid type {type}");
//             }
//         }
//     }
// }
