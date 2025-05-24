using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Transactions;

namespace Libplanet.Net;

public partial class Swarm
{
    private readonly NullableSemaphore _transferBlocksSemaphore;
    private readonly NullableSemaphore _transferTxsSemaphore;
    private readonly NullableSemaphore _transferEvidenceSemaphore;

    private Task ProcessMessageHandlerAsync(Message message)
    {
        switch (message.Content)
        {
            case PingMessage _:
            case FindNeighborsMessage _:
                return Task.CompletedTask;

            case GetChainStatusMessage getChainStatus:
                {
                    _logger.Debug(
                        "Received a {MessageType} message",
                        nameof(GetChainStatusMessage));

                    // This is based on the assumption that genesis block always exists.
                    Block tip = BlockChain.Tip;
                    var chainStatus = new ChainStatusMessage(
                        tip.Version,
                        BlockChain.Genesis.BlockHash,
                        tip.Height,
                        tip.BlockHash);

                    return Transport.ReplyMessageAsync(
                        chainStatus,
                        message.Identity,
                        default);
                }

            case GetBlockHashesMessage getBlockHashes:
                {
                    _logger.Debug(
                        "Received a {MessageType} message locator [{LocatorHead}]",
                        nameof(GetBlockHashesMessage),
                        getBlockHashes.BlockHash);
                    var height = BlockChain.Blocks[getBlockHashes.BlockHash].Height;
                    var hashes = BlockChain.Blocks[height..].Select(item => item.BlockHash).ToArray();

                    // IReadOnlyList<BlockHash> hashes = BlockChain.FindNextHashes(
                    //     getBlockHashes.Locator,
                    //     FindNextHashesChunkSize);
                    _logger.Debug(
                        "Found {HashCount} hashes after the branchpoint " +
                        "with locator [{LocatorHead}]",
                        hashes.Length,
                        getBlockHashes.BlockHash);
                    var reply = new BlockHashesMessage(hashes);

                    return Transport.ReplyMessageAsync(reply, message.Identity, default);
                }

            case GetBlocksMessage getBlocksMsg:
                return TransferBlocksAsync(message);

            case GetTransactionMessage getTxs:
                return TransferTxsAsync(message);

            case GetEvidenceMessage getTxs:
                return TransferEvidenceAsync(message);

            case TxIdsMessage txIds:
                ProcessTxIds(message);
                return Transport.ReplyMessageAsync(
                    new PongMessage(),
                    message.Identity,
                    default);

            case EvidenceIdsMessage evidenceIds:
                ProcessEvidenceIds(message);
                return Transport.ReplyMessageAsync(
                    new PongMessage(),
                    message.Identity,
                    default);

            case BlockHashesMessage _:
                _logger.Error(
                    "{MessageType} messages are only for IBD",
                    nameof(BlockHashesMessage));
                return Task.CompletedTask;

            case BlockHeaderMessage blockHeader:
                ProcessBlockHeader(message);
                return Transport.ReplyMessageAsync(
                    new PongMessage(),
                    message.Identity,
                    default);

            default:
                throw new InvalidMessageContentException(
                    $"Failed to handle message: {message.Content}",
                    message.Content);
        }
    }

    private void ProcessBlockHeader(Message message)
    {
        var blockHeaderMsg = (BlockHeaderMessage)message.Content;
        if (!blockHeaderMsg.GenesisHash.Equals(BlockChain.Genesis.BlockHash))
        {
            _logger.Debug(
                "{MessageType} message was sent from a peer {Peer} with " +
                "a different genesis block {Hash}",
                nameof(BlockHeaderMessage),
                message.Remote,
                blockHeaderMsg.GenesisHash);
            return;
        }

        BlockHeaderReceived.Set();
        BlockExcerpt header;
        try
        {
            header = blockHeaderMsg.GetHeader();
        }
        catch (InvalidOperationException ibe)
        {
            _logger.Debug(
                ibe,
                "Received header #{BlockHeight} {BlockHash} is invalid",
                blockHeaderMsg.HeaderHash,
                blockHeaderMsg.HeaderIndex);
            return;
        }

        try
        {
            header.Timestamp.ValidateTimestamp();
        }
        catch (InvalidOperationException e)
        {
            _logger.Debug(
                e,
                "Received header #{BlockHeight} {BlockHash} has invalid timestamp: {Timestamp}",
                header.Height,
                header.BlockHash,
                header.Timestamp);
            return;
        }

        bool needed = IsBlockNeeded(header);
        _logger.Information(
            "Received " + nameof(BlockHeader) + " #{ReceivedIndex} {ReceivedHash}",
            header.Height,
            header.BlockHash);

        if (needed)
        {
            _logger.Information(
                "Adding received header #{BlockHeight} {BlockHash} from peer {Peer} to " +
                nameof(BlockDemandTable) + "...",
                header.Height,
                header.BlockHash,
                message.Remote);
            BlockDemandTable.Add(
                BlockChain,
                IsBlockNeeded,
                new BlockDemand(header, message.Remote, DateTimeOffset.UtcNow));
            return;
        }
        else
        {
            _logger.Information(
                "Discarding received header #{ReceivedIndex} {ReceivedHash} from peer {Peer} " +
                "as it is not needed for the current chain with tip #{TipIndex} {TipHash}",
                header.Height,
                header.BlockHash,
                message.Remote,
                BlockChain.Tip.Height,
                BlockChain.Tip.BlockHash);
            return;
        }
    }

    private async Task TransferTxsAsync(Message message)
    {
        if (!await _transferTxsSemaphore.WaitAsync(TimeSpan.Zero, _cancellationToken))
        {
            _logger.Debug(
                "Message {Message} is dropped due to task limit {Limit}",
                message,
                Options.TaskRegulationOptions.MaxTransferTxsTaskCount);
            return;
        }

        try
        {
            var getTxsMsg = (GetTransactionMessage)message.Content;
            foreach (TxId txid in getTxsMsg.TxIds)
            {
                try
                {
                    Transaction tx = BlockChain.Transactions[txid];

                    if (tx is null)
                    {
                        continue;
                    }

                    MessageContent response = new TransactionMessage(ModelSerializer.SerializeToBytes(tx));
                    await Transport.ReplyMessageAsync(response, message.Identity, default);
                }
                catch (KeyNotFoundException)
                {
                    _logger.Warning("Requested TxId {TxId} does not exist", txid);
                }
            }
        }
        finally
        {
            int count = _transferTxsSemaphore.Release();
            if (count >= 0)
            {
                _logger.Debug(
                    "{Count}/{Limit} tasks are remaining for handling {FName}",
                    count,
                    Options.TaskRegulationOptions.MaxTransferTxsTaskCount,
                    nameof(TransferTxsAsync));
            }
        }
    }

    private void ProcessTxIds(Message message)
    {
        var txIdsMsg = (TxIdsMessage)message.Content;
        _logger.Information(
            "Received a {MessageType} message with {TxIdCount} txIds",
            nameof(TxIdsMessage),
            txIdsMsg.Ids.Count());

        TxCompletion.Demand(message.Remote, txIdsMsg.Ids);
    }

    private async Task TransferBlocksAsync(Message message)
    {
        if (!await _transferBlocksSemaphore.WaitAsync(TimeSpan.Zero, _cancellationToken))
        {
            _logger.Debug(
                "Message {Message} is dropped due to task limit {Limit}",
                message,
                Options.TaskRegulationOptions.MaxTransferBlocksTaskCount);
            return;
        }

        try
        {
            var blocksMsg = (GetBlocksMessage)message.Content;
            string reqId = !(message.Identity is null) && message.Identity.Length == 16
                ? new Guid(message.Identity).ToString()
                : "unknown";
            _logger.Verbose(
                "Preparing a {MessageType} message to reply to {Identity}...",
                nameof(Messages.BlocksMessage),
                reqId);

            var payloads = new List<byte[]>();

            List<BlockHash> hashes = blocksMsg.BlockHashes.ToList();
            int count = 0;
            int total = hashes.Count;
            const string logMsg =
                "Fetching block {Index}/{Total} {Hash} to include in " +
                "a reply to {Identity}...";
            foreach (BlockHash hash in hashes)
            {
                _logger.Verbose(logMsg, count, total, hash, reqId);
                if (BlockChain.Blocks.TryGetValue(hash, out var block))
                {
                    byte[] blockPayload = ModelSerializer.SerializeToBytes(block);
                    payloads.Add(blockPayload);
                    byte[] commitPayload = BlockChain.BlockCommits[block.BlockHash] is { } commit
                        ? ModelSerializer.SerializeToBytes(commit)
                        : Array.Empty<byte>();
                    payloads.Add(commitPayload);
                    count++;
                }

                if (payloads.Count / 2 == blocksMsg.ChunkSize)
                {
                    var response = new BlocksMessage(payloads);
                    _logger.Verbose(
                        "Enqueuing a blocks reply (...{Count}/{Total})...",
                        count,
                        total);
                    await Transport.ReplyMessageAsync(response, message.Identity, default);
                    payloads.Clear();
                }
            }

            if (payloads.Any())
            {
                var response = new BlocksMessage(payloads);
                _logger.Verbose(
                    "Enqueuing a blocks reply (...{Count}/{Total}) to {Identity}...",
                    count,
                    total,
                    reqId);
                await Transport.ReplyMessageAsync(response, message.Identity, default);
            }

            if (count == 0)
            {
                var response = new BlocksMessage(payloads);
                _logger.Verbose(
                    "Enqueuing a blocks reply (...{Index}/{Total}) to {Identity}...",
                    count,
                    total,
                    reqId);
                await Transport.ReplyMessageAsync(response, message.Identity, default);
            }

            _logger.Debug("{Count} blocks were transferred to {Identity}", count, reqId);
        }
        finally
        {
            int count = _transferBlocksSemaphore.Release();
            if (count >= 0)
            {
                _logger.Debug(
                    "{Count}/{Limit} tasks are remaining for handling {FName}",
                    count,
                    Options.TaskRegulationOptions.MaxTransferBlocksTaskCount,
                    nameof(TransferBlocksAsync));
            }
        }
    }
}
