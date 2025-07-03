using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Tasks;

internal sealed class BlockBroadcastTask : SwarmTaskBase, IDisposable
{
    private readonly Swarm _swarm;
    private readonly IDisposable _tipChangedSubscription;

    public BlockBroadcastTask(Swarm swarm)
    {
        _swarm = swarm;
        _tipChangedSubscription = swarm.Blockchain.TipChanged.Subscribe(e =>
        {
            BroadcastBlock(default, e.Tip);
        });
    }

    protected override TimeSpan Interval => _swarm.Options.BlockBroadcastInterval;

    public void Dispose()
    {
        _tipChangedSubscription.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var blockchain = _swarm.Blockchain;
        BroadcastBlock(default, blockchain.Tip);
        await Task.CompletedTask;
    }

    private void BroadcastBlock(Address except, Block block)
    {
        var blockchain = _swarm.Blockchain;
        var message = new BlockHeaderMessage
        {
            GenesisHash = blockchain.Genesis.BlockHash,
            BlockSummary = block
        };
        _swarm.BroadcastMessage(except, message);
    }
}
