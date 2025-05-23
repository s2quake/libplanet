using Libplanet.State;
using Libplanet;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet.Node.Tests;

internal static class BlockChainUtility
{
    public static Task<Block> AppendBlockAsync(Blockchain blockChain)
        => AppendBlockAsync(blockChain, new PrivateKey());

    public static async Task<Block> AppendBlockAsync(Blockchain blockChain, PrivateKey privateKey)
    {
        var tip = blockChain.Tip;
        var height = tip.Height + 1;
        var block = blockChain.ProposeBlock(proposer: privateKey);
        blockChain.Append(
            block,
            blockChain.BlockCommits[tip.BlockHash]);

        while (blockChain.Tip.Height < height)
        {
            await Task.Delay(100);
        }

        await Task.Delay(1000);

        return block;
    }

    public static void StageTransaction(
        Blockchain blockChain, IAction[] actions)
        => StageTransaction(blockChain, new PrivateKey(), actions);

    public static void StageTransaction(
        Blockchain blockChain, PrivateKey privateKey, IAction[] actions)
    {
        var transaction = CreateTransaction(blockChain, privateKey, actions);
        blockChain.StagedTransactions.Add(transaction);
    }

    public static Transaction CreateTransaction(
        Blockchain blockChain, IAction[] actions)
        => CreateTransaction(blockChain, new PrivateKey(), actions);

    public static Transaction CreateTransaction(
        Blockchain blockChain, PrivateKey privateKey, IAction[] actions)
    {
        var genesisBlock = blockChain.Genesis;
        var nonce = blockChain.GetNextTxNonce(privateKey.Address);
        var values = actions.ToBytecodes();
        return new TransactionMetadata
        {
            Nonce = nonce,
            Signer = privateKey.Address,
            GenesisHash = genesisBlock.BlockHash,
            Actions = values,
        }.Sign(privateKey);
    }
}
