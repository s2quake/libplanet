// using Libplanet.Action;
// using Libplanet.Types.Blocks;
// using Libplanet.Types.Tx;

// namespace Libplanet.Blockchain.Policies;

// public interface BlockPolicy
// {
//     PolicyActions PolicyActions { get; }

//     InvalidOperationException? ValidateNextBlockTx(BlockChain blockChain, Transaction transaction);

//     Exception? ValidateNextBlock(BlockChain blockChain, Block nextBlock);

//     long MaxTransactionsBytes(long index);

//     int MinTransactionsPerBlock(long index);

//     int MaxTransactionsPerBlock(long index);

//     int GetMaxTransactionsPerSignerPerBlock(long index);

//     long GetMaxEvidencePendingDuration(long index);
// }
