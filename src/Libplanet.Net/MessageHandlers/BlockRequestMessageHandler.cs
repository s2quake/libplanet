using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockRequestMessageHandler(Blockchain blockchain, ITransport transport, int maxAccessCount)
    : MessageHandlerBase<BlockRequestMessage>, IDisposable
{
    private readonly Blockchain _blockchain = blockchain;
    private readonly ITransport _transport = transport;
    private readonly AccessLimiter _accessLimiter = new(maxAccessCount);

    internal BlockRequestMessageHandler(Swarm swarm, SwarmOptions options)
        : this(swarm.Blockchain, swarm.Transport, options.TaskRegulationOptions.MaxTransferBlocksTaskCount)
    {
    }

    public void Dispose() => _accessLimiter.Dispose();

    protected override void OnHandle(BlockRequestMessage message, MessageEnvelope messageEnvelope)
    {
        _ = Task.Run(async () => await OnHandleAsync(message, messageEnvelope, default));
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
                await Task.Yield();
            }
        }

        var lastResponse = new BlockResponseMessage
        {
            Blocks = [.. blockList],
            BlockCommits = [.. blockCommitList],
            IsLast = true,
        };
        _transport.Post(messageEnvelope.Sender, lastResponse, messageEnvelope.Identity);
        await Task.Yield();
    }
}
