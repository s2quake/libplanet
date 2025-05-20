using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    public static Block ProposeGenesisBlock(
        PrivateKey proposer, ImmutableSortedSet<Transaction> transactions, HashDigest<SHA256> stateRootHash = default)
    {
        var blockHeader = new BlockHeader
        {
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
        };
        var blockContent = new BlockContent
        {
            Transactions = transactions,
            Evidences = [],
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer, stateRootHash);
    }

    public Block ProposeBlock(PrivateKey proposer)
    {
        var height = Blocks.Count;
        var transactions = StagedTransactions.Collect();
        var evidences = PendingEvidences.Collect();
        var previousHash = _repository.Chain.BlockHashes[height - 1];
        var stateRootHash = GetNextStateRootHash(previousHash) ??
            throw new InvalidOperationException(
                $"Cannot propose a block as the next state root hash " +
                $"for block {previousHash} is missing.");

        var blockHeader = new BlockHeader
        {
            Height = height,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousHash = previousHash,
            LastCommit = BlockCommits[height - 1],
        };
        var blockContent = new BlockContent
        {
            Transactions = transactions,
            Evidences = evidences,
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer, stateRootHash);
    }
}
