using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockRequestMessageHandler(Swarm swarm, SwarmOptions options)
    : MessageHandlerBase<BlockRequestMessage>, IDisposable
{
    private readonly Blockchain _blockchain = swarm.Blockchain;
    private readonly ITransport _transport = swarm.Transport;
    private readonly AccessLimiter _accessLimiter = new(options.TaskRegulationOptions.MaxTransferBlocksTaskCount);

    public void Dispose()
    {
        _accessLimiter.Dispose();
    }

    protected override void OnHandle(
        BlockRequestMessage message, MessageEnvelope messageEnvelope)
    {
        _ = OnHandleAsync(message, messageEnvelope, default).AsTask();
    }

    private async ValueTask OnHandleAsync(
        BlockRequestMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        using var scope = await _accessLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var blockHashes = message.BlockHashes;
        var blockList = new List<Block>();
        var blockCommitList = new List<BlockCommit>();
        foreach (var blockHash in blockHashes)
        {
            if (_blockchain.Blocks.TryGetValue(blockHash, out var block)
                && _blockchain.BlockCommits.TryGetValue(block.BlockHash, out var blockCommit))
            {
                blockList.Add(block);
                blockCommitList.Add(blockCommit);
            }
            else
            {
                int qewr = 0;
            }

            if (blockList.Count == message.ChunkSize)
            {
                var response = new BlockResponseMessage
                {
                    Blocks = [.. blockList],
                    BlockCommits = [.. blockCommitList],
                };
                _transport.Post(messageEnvelope.Sender, response, messageEnvelope.Identity);
                blockList.Clear();
                blockCommitList.Clear();
            }
        }

        var lastResponse = new BlockResponseMessage
        {
            Blocks = [.. blockList],
            BlockCommits = [.. blockCommitList],
            IsLast = true,
        };
        _transport.Post(messageEnvelope.Sender, lastResponse, messageEnvelope.Identity);
    }
}
