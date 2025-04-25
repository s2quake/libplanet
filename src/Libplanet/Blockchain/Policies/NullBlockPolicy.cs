#nullable disable
using Libplanet.Action;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain.Policies
{
    public class NullBlockPolicy : IBlockPolicy
    {
        private readonly Exception _exceptionToThrow;

        public NullBlockPolicy(
            Exception exceptionToThrow = null)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public ISet<Address> BlockedMiners { get; } = new HashSet<Address>();

        public IPolicyActionsRegistry PolicyActionsRegistry => new PolicyActionsRegistry();

        public ImmutableArray<IAction> BeginBlockActions => ImmutableArray<IAction>.Empty;

        public ImmutableArray<IAction> EndBlockActions => ImmutableArray<IAction>.Empty;

        public ImmutableArray<IAction> BeginTxActions => ImmutableArray<IAction>.Empty;

        public ImmutableArray<IAction> EndTxActions => ImmutableArray<IAction>.Empty;

        public int GetMinTransactionsPerBlock(long index) => 0;

        public int GetMaxTransactionsPerBlock(long index) => int.MaxValue;

        public virtual InvalidOperationException ValidateNextBlockTx(
            BlockChain blockChain, Transaction transaction) => null;

        public virtual Exception ValidateNextBlock(
            BlockChain blockChain,
            Block nextBlock
        )
        {
            if (_exceptionToThrow != null)
            {
                return _exceptionToThrow;
            }

            return BlockedMiners.Contains(nextBlock.Miner)
                ? new Exception(
                    $"Disallowed #{nextBlock.Index} {nextBlock.Hash} mined by {nextBlock.Miner}.")
                : null;
        }

        public long GetMaxTransactionsBytes(long index) => 1024 * 1024;

        public int GetMaxTransactionsPerSignerPerBlock(long index) =>
            GetMaxTransactionsPerBlock(index);

        public long GetMaxEvidencePendingDuration(long index) => 10L;
    }
}
