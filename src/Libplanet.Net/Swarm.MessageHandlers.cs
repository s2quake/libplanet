using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

public partial class Swarm
{
    private readonly NullableSemaphore _transferBlocksSemaphore;
    private readonly NullableSemaphore _transferTxsSemaphore;
    private readonly NullableSemaphore _transferEvidenceSemaphore;

    private void ProcessMessageHandler(MessageEnvelope message)
    {
        switch (message.Message)
        {
            case PingMessage _:
            case FindNeighborsMessage _:
                return;

            case GetChainStatusMessage getChainStatus:
                {
                    _logger.Debug(
                        "Received a {MessageType} message",
                        nameof(GetChainStatusMessage));

                    // This is based on the assumption that genesis block always exists.
                    Block tip = Blockchain.Tip;
                    var chainStatus = new ChainStatusMessage
                    {
                        ProtocolVersion = tip.Version,
                        GenesisHash = Blockchain.Genesis.BlockHash,
                        TipIndex = tip.Height,
                        TipHash = tip.BlockHash,
                    };

                    Transport.ReplyMessage(message.Identity, chainStatus);
                }
                break;

            case GetBlockHashesMessage getBlockHashes:
                {
                    _logger.Debug(
                        "Received a {MessageType} message locator [{LocatorHead}]",
                        nameof(GetBlockHashesMessage),
                        getBlockHashes.BlockHash);
                    var height = Blockchain.Blocks[getBlockHashes.BlockHash].Height;
                    var hashes = Blockchain.Blocks[height..].Select(item => item.BlockHash).ToArray();

                    // IReadOnlyList<BlockHash> hashes = BlockChain.FindNextHashes(
                    //     getBlockHashes.Locator,
                    //     FindNextHashesChunkSize);
                    _logger.Debug(
                        "Found {HashCount} hashes after the branchpoint " +
                        "with locator [{LocatorHead}]",
                        hashes.Length,
                        getBlockHashes.BlockHash);
                    var reply = new BlockHashesMessage { Hashes = [.. hashes] };

                    Transport.ReplyMessage(message.Identity, reply);
                }
                break;

            case GetBlocksMessage getBlocksMsg:
                TransferBlocksAsync(message);
                break;

            case GetTransactionMessage getTxs:
                TransferTxsAsync(message);
                break;

            case GetEvidenceMessage getTxs:
                TransferEvidenceAsync(message);
                break;

            case TxIdsMessage txIds:
                ProcessTxIds(message);
                Transport.ReplyMessage(message.Identity, new PongMessage());
                break;

            case EvidenceIdsMessage evidenceIds:
                ProcessEvidenceIds(message);
                Transport.ReplyMessage(message.Identity, new PongMessage());
                break;

            case BlockHashesMessage _:
                _logger.Error(
                    "{MessageType} messages are only for IBD",
                    nameof(BlockHashesMessage));
                break;

            case BlockHeaderMessage blockHeader:
                ProcessBlockHeader(message);
                Transport.ReplyMessage(message.Identity, new PongMessage());
                break;

            default:
                throw new InvalidOperationException($"Failed to handle message: {message.Message}");
        }
    }

    private void ProcessBlockHeader(MessageEnvelope message)
    {
        var blockHeaderMsg = (BlockHeaderMessage)message.Message;
        if (!blockHeaderMsg.GenesisHash.Equals(Blockchain.Genesis.BlockHash))
        {
            _logger.Debug(
                "{MessageType} message was sent from a peer {Peer} with " +
                "a different genesis block {Hash}",
                nameof(BlockHeaderMessage),
                message.Peer,
                blockHeaderMsg.GenesisHash);
            return;
        }

        BlockHeaderReceived.Set();
        BlockExcerpt header;
        try
        {
            header = blockHeaderMsg.Excerpt;
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
                message.Peer);
            BlockDemandTable.Add(
                IsBlockNeeded,
                new BlockDemand(header, message.Peer, DateTimeOffset.UtcNow));
            return;
        }
        else
        {
            _logger.Information(
                "Discarding received header #{ReceivedIndex} {ReceivedHash} from peer {Peer} " +
                "as it is not needed for the current chain with tip #{TipIndex} {TipHash}",
                header.Height,
                header.BlockHash,
                message.Peer,
                Blockchain.Tip.Height,
                Blockchain.Tip.BlockHash);
            return;
        }
    }

    private async Task TransferTxsAsync(MessageEnvelope message)
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
            var getTxsMsg = (GetTransactionMessage)message.Message;
            foreach (TxId txid in getTxsMsg.TxIds)
            {
                try
                {
                    Transaction tx = Blockchain.Transactions[txid];

                    if (tx is null)
                    {
                        continue;
                    }

                    MessageBase response = new TransactionMessage { Payload = ModelSerializer.SerializeToBytes(tx) };
                    Transport.ReplyMessage(message.Identity, response);
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

    private void ProcessTxIds(MessageEnvelope message)
    {
        var txIdsMsg = (TxIdsMessage)message.Message;
        _logger.Information(
            "Received a {MessageType} message with {TxIdCount} txIds",
            nameof(TxIdsMessage),
            txIdsMsg.Ids.Count());

        TxCompletion.DemandMany(message.Peer, txIdsMsg.Ids);
    }

    private async void TransferBlocksAsync(MessageEnvelope message)
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
            var blocksMsg = (GetBlocksMessage)message.Message;
            // string reqId = !(message.Id is null) && message.Id.Length == 16
            //     ? new Guid(message.Id).ToString()
            //     : "unknown";
            // _logger.Verbose(
            //     "Preparing a {MessageType} message to reply to {Identity}...",
            //     nameof(Messages.BlocksMessage),
            //     reqId);

            var payloads = new List<byte[]>();

            List<BlockHash> hashes = blocksMsg.BlockHashes.ToList();
            int count = 0;
            int total = hashes.Count;
            const string logMsg =
                "Fetching block {Index}/{Total} {Hash} to include in " +
                "a reply to {Identity}...";
            foreach (BlockHash hash in hashes)
            {
                // _logger.Verbose(logMsg, count, total, hash, reqId);
                if (Blockchain.Blocks.TryGetValue(hash, out var block))
                {
                    byte[] blockPayload = ModelSerializer.SerializeToBytes(block);
                    payloads.Add(blockPayload);
                    byte[] commitPayload = Blockchain.BlockCommits[block.BlockHash] is { } commit
                        ? ModelSerializer.SerializeToBytes(commit)
                        : Array.Empty<byte>();
                    payloads.Add(commitPayload);
                    count++;
                }

                if (payloads.Count / 2 == blocksMsg.ChunkSize)
                {
                    var response = new BlocksMessage { Payloads = [.. payloads] };
                    _logger.Verbose(
                        "Enqueuing a blocks reply (...{Count}/{Total})...",
                        count,
                        total);
                    Transport.ReplyMessage(message.Identity, response);
                    payloads.Clear();
                }
            }

            if (payloads.Any())
            {
                var response = new BlocksMessage { Payloads = [.. payloads] };
                // _logger.Verbose(
                //     "Enqueuing a blocks reply (...{Count}/{Total}) to {Identity}...",
                //     count,
                //     total,
                //     reqId);
                Transport.ReplyMessage(message.Identity, response);
            }

            if (count == 0)
            {
                var response = new BlocksMessage { Payloads = [.. payloads] };
                // _logger.Verbose(
                //     "Enqueuing a blocks reply (...{Index}/{Total}) to {Identity}...",
                //     count,
                //     total,
                //     reqId);
                Transport.ReplyMessage(message.Identity, response);
            }

            // _logger.Debug("{Count} blocks were transferred to {Identity}", count, reqId);
        }
        finally
        {
            int count = _transferBlocksSemaphore.Release();
            if (count >= 0)
            {
                // _logger.Debug(
                //     "{Count}/{Limit} tasks are remaining for handling {FName}",
                //     count,
                //     Options.TaskRegulationOptions.MaxTransferBlocksTaskCount,
                //     nameof(TransferBlocksAsync));
            }
        }
    }
}
