// using Libplanet.Action;
// using Libplanet.Types.Blocks;
// using Libplanet.Types.Crypto;
// using Libplanet.Types.Tx;

// namespace Libplanet.Blockchain.Policies;

// public class NullBlockPolicy : BlockPolicy
// {
//     private readonly Exception _exceptionToThrow;

//     public NullBlockPolicy(
//         Exception exceptionToThrow = null)
//     {
//         _exceptionToThrow = exceptionToThrow;
//     }

//     public ISet<Address> BlockedMiners { get; } = new HashSet<Address>();

//     public PolicyActions PolicyActions => new PolicyActions();

//     public ImmutableArray<IAction> BeginBlockActions => ImmutableArray<IAction>.Empty;

//     public ImmutableArray<IAction> EndBlockActions => ImmutableArray<IAction>.Empty;

//     public ImmutableArray<IAction> BeginTxActions => ImmutableArray<IAction>.Empty;

//     public ImmutableArray<IAction> EndTxActions => ImmutableArray<IAction>.Empty;

//     public int MinTransactionsPerBlock(long index) => 0;

//     public int MaxTransactionsPerBlock(long index) => int.MaxValue;

//     public virtual InvalidOperationException ValidateNextBlockTx(
//         BlockChain blockChain, Transaction transaction) => null;

//     public virtual Exception ValidateNextBlock(
//         BlockChain blockChain,
//         Block nextBlock)
//     {
//         if (_exceptionToThrow != null)
//         {
//             return _exceptionToThrow;
//         }

//         return BlockedMiners.Contains(nextBlock.Proposer)
//             ? new Exception(
//                 $"Disallowed #{nextBlock.Height} {nextBlock.BlockHash} mined by {nextBlock.Proposer}.")
//             : null;
//     }

//     public long MaxTransactionsBytes(long index) => 1024 * 1024;

//     public int GetMaxTransactionsPerSignerPerBlock(long index) =>
//         MaxTransactionsPerBlock(index);

//     public long GetMaxEvidencePendingDuration(long index) => 10L;
// }
