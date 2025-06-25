// using Libplanet.Net.Consensus;
// using Libplanet.Types;

// namespace Libplanet.Net.Consensus;

// public interface IConsensusMediator
// {
//     IObservable<Block> BlockProposed { get; }

//     IObservable<BlockHash> PreVoteEntered { get; }

//     IObservable<BlockHash> PreCommitEntered { get; }

//     Address Signer { get; }

//     Block ProposeBlock();

//     void ValidateBlock(Block block);

//     void OnBlockPropose(Block block);

//     void OnPreVoteEnter(BlockHash blockHash);

//     void OnPreCommitEnter(BlockHash blockHash);
// }

// public sealed class ConsensusMediator : IConsensusMediator
// {
//     private readonly Blockchain _blockchain;
//     private readonly ISigner _signer;

//     public ConsensusMediator(Blockchain blockchain, ISigner signer, Gossip gossip)
//     {
//         _blockchain = blockchain;
//     }

//     public IObservable<Block> BlockProposed => throw new NotImplementedException();

//     public IObservable<BlockHash> PreVoteEntered => throw new NotImplementedException();

//     public IObservable<BlockHash> PreCommitEntered => throw new NotImplementedException();

//     public Address Signer => throw new NotImplementedException();

//     public void OnBlockPropose(Block block)
//     {
//         var proposal = new ProposalMetadata
//             {
//                 BlockHash = e.Block.BlockHash,
//                 Height = Height,
//                 Round = Round,
//                 Timestamp = DateTimeOffset.UtcNow,
//                 Proposer = _privateKey.Address,
//                 ValidRound = e.ValidRound,
//             }.Sign(_signer, block);
//             var message = new ConsensusProposalMessage { Proposal = proposal };
//             _gossip.PublishMessage(message);
//     }

//     public void OnPreCommitEnter(BlockHash blockHash)
//     {
//         throw new NotImplementedException();
//     }

//     public void OnPreVoteEnter(BlockHash blockHash)
//     {
//         throw new NotImplementedException();
//     }

//     public Block ProposeBlock()
//     {
//         return _blockchain.ProposeBlock(_signer);
//     }

//     public void ValidateBlock(Block block)
//     {
//         throw new NotImplementedException();
//     }
// }
