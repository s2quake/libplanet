using System.Security.Cryptography;
using Libplanet.State;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet;

public partial class Blockchain
{
    public static Block ProposeGenesisBlock(
        PrivateKey proposer,
        ImmutableSortedSet<Transaction> transactions,
        HashDigest<SHA256> previousStateRootHash = default)
    {
        var blockHeader = new BlockHeader
        {
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousStateRootHash = previousStateRootHash,
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
        return rawBlock.Sign(proposer);
    }

    public static Block ProposeGenesisBlock(
        PrivateKey proposer,
        ImmutableArray<IAction> actions,
        HashDigest<SHA256> previousStateRootHash = default)
    {
        var transaction = new TransactionMetadata
        {
            Signer = proposer.Address,
            Actions = actions.ToBytecodes(),
            Timestamp = DateTimeOffset.UtcNow,
        }.Sign(proposer);
        var blockHeader = new BlockHeader
        {
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousStateRootHash = previousStateRootHash,
        };
        var blockContent = new BlockContent
        {
            Transactions = [transaction],
            Evidences = [],
        };
        var rawBlock = new RawBlock
        {
            Header = blockHeader,
            Content = blockContent,
        };
        return rawBlock.Sign(proposer);
    }

    public Block ProposeBlock(PrivateKey proposer)
    {
        var tip = Tip;
        var height = tip.Height + 1;
        var transactions = StagedTransactions.Collect();
        var evidences = PendingEvidences.Collect();
        var previousHash = tip.BlockHash;
        var blockHeader = new BlockHeader
        {
            Height = height,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousHash = previousHash,
            PreviousCommit = BlockCommits[previousHash],
            PreviousStateRootHash = GetStateRootHash(previousHash),
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
        return rawBlock.Sign(proposer);
    }
}
