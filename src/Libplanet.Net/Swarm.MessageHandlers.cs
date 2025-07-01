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
                    var height = Blockchain.Blocks[getBlockHashes.BlockHash].Height;
                    var hashes = Blockchain.Blocks[height..].Select(item => item.BlockHash).ToArray();

                    // IReadOnlyList<BlockHash> hashes = BlockChain.FindNextHashes(
                    //     getBlockHashes.Locator,
                    //     FindNextHashesChunkSize);
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
            return;
        }

        try
        {
            header.Timestamp.ValidateTimestamp();
        }
        catch (InvalidOperationException e)
        {
            return;
        }

        bool needed = IsBlockNeeded(header);
        if (needed)
        {
            BlockDemandTable.Add(
                IsBlockNeeded,
                new BlockDemand(header, message.Peer, DateTimeOffset.UtcNow));
            return;
        }
        else
        {
            return;
        }
    }

    private async Task TransferTxsAsync(MessageEnvelope message)
    {
        if (!await _transferTxsSemaphore.WaitAsync(TimeSpan.Zero, _cancellationToken))
        {
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

                    MessageBase response = new TransactionMessage
                    {
                        Payload = [.. ModelSerializer.SerializeToBytes(tx)],
                    };
                    Transport.ReplyMessage(message.Identity, response);
                }
                catch (KeyNotFoundException)
                {
                }
            }
        }
        finally
        {
            int count = _transferTxsSemaphore.Release();
            if (count >= 0)
            {
            }
        }
    }

    private void ProcessTxIds(MessageEnvelope message)
    {
        var txIdsMsg = (TxIdsMessage)message.Message;
        TxCompletion.DemandMany(message.Peer, txIdsMsg.Ids);
    }

    private async void TransferBlocksAsync(MessageEnvelope message)
    {
        if (!await _transferBlocksSemaphore.WaitAsync(TimeSpan.Zero, _cancellationToken))
        {
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
