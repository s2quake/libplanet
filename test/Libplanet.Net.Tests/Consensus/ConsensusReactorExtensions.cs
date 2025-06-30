using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Tests.Consensus;

public static class ConsensusReactorExtensions
{
    public static async Task WaitUntilAsync(
        this ConsensusReactor @this, int height, CancellationToken cancellationToken)
    {
        if (@this.Height > height)
        {
            throw new InvalidOperationException($"Height {height} is already passed.");
        }

        using var manualResetEvent = new ManualResetEvent(false);
        using var _1 = @this.HeightChanged.Subscribe(height =>
        {
            if (height == @this.Height)
            {
                manualResetEvent.Set();
            }
        });

        while (true)
        {
            if (manualResetEvent.WaitOne(0))
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    public static async Task WaitUntilAsync(
        this ConsensusReactor @this, int height, ConsensusStep step, CancellationToken cancellationToken)
    {
        using var resetvent = new ManualResetEvent(false);

        var consensus = @this.Consensus;
        var stepChangedSubscription = @this.Consensus.StepChanged.Subscribe(Consensus_StepChanged);

        try
        {
            using var _2 = @this.HeightChanged.Subscribe(consensus =>
            {
                stepChangedSubscription.Dispose();
                stepChangedSubscription = @this.Consensus.StepChanged.Subscribe(Consensus_StepChanged);
            });

            while (true)
            {
                if (resetvent.WaitOne(0))
                {
                    return;
                }

                await Task.Delay(100, cancellationToken);
            }
        }
        finally
        {
            stepChangedSubscription.Dispose();
        }

        void Consensus_StepChanged(ConsensusStep e)
        {
            if (e == step && consensus.Height == height)
            {
                resetvent.Set();
            }
        }
    }

    public static async Task<T> WaitUntilPublishedAsync<T>(
        this ConsensusReactor @this,
        int height,
        CancellationToken cancellationToken)
        where T : ConsensusMessage
    {
        T? consensusMessage = null;
        // var asyncAutoResetEvent = new AsyncAutoResetEvent();
        // @this.MessagePublished += ConsensusContext_MessagePublished;
        // try
        // {
        //     await asyncAutoResetEvent.WaitAsync(cancellationToken);
        //     return consensusMessage!;
        // }
        // finally
        // {
        //     // @this.MessagePublished -= ConsensusContext_MessagePublished;
        // }
        throw new NotImplementedException();

        void ConsensusContext_MessagePublished(
            object? sender, (int Height, ConsensusMessage Message) e)
        {
            if (e.Message is T { } message && e.Height == height)
            {
                consensusMessage = message;
                // asyncAutoResetEvent.Set();
            }
        }
    }
}
