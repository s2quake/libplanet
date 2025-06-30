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
        using var heightChangedEvent = new ManualResetEvent(false);
        using var stepChangedEvent = new ManualResetEvent(false);
        using var _1 = @this.HeightChanged.Subscribe(height =>
        {
            if (height == @this.Height)
            {
                heightChangedEvent.Set();
            }
        });
        // using var _2 = @this.Step.Subscribe(state =>
        // {
        //     if (state.Step == step && state.Height == height)
        //     {
        //         stepChangedEvent.Set();
        //     }
        // });
        // var asyncAutoResetEvent = new AsyncAutoResetEvent();
        // @this.StateChanged += ConsensusContext_StateChanged;
        // try
        // {
        //     if (@this.Step != step || @this.Height != height)
        //     {
        //         await asyncAutoResetEvent.WaitAsync(cancellationToken);
        //     }
        // }
        // finally
        // {
        //     // @this.StateChanged -= ConsensusContext_StateChanged;
        // }

        // void ConsensusContext_StateChanged(object? sender, ConsensusState e)
        // {
        //     if (e.Step == consensusStep && e.Height == height)
        //     {
        //         asyncAutoResetEvent.Set();
        //     }
        // }
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
