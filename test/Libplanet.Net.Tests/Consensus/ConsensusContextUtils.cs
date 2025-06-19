using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Nito.AsyncEx;

namespace Libplanet.Net.Tests.Consensus
{
    public static class ConsensusContextUtils
    {
        public static async Task WaitUntilHeightAsync(
            ConsensusReactor consensusContext,
            int height,
            CancellationToken cancellationToken)
        {
            if (consensusContext.Height > height)
            {
                throw new InvalidOperationException($"Height {height} is already passed.");
            }

            while (true)
            {
                if (consensusContext.Height == height)
                {
                    return;
                }

                await Task.Delay(100, cancellationToken);
            }
        }

        public static async Task WaitUntilStateAsync(
            ConsensusReactor consensusContext,
            int height,
            ConsensusStep consensusStep,
            CancellationToken cancellationToken)
        {
            var asyncAutoResetEvent = new AsyncAutoResetEvent();
            consensusContext.StateChanged += ConsensusContext_StateChanged;
            try
            {
                if (consensusContext.Step != consensusStep || consensusContext.Height != height)
                {
                    await asyncAutoResetEvent.WaitAsync(cancellationToken);
                }
            }
            finally
            {
                consensusContext.StateChanged -= ConsensusContext_StateChanged;
            }

            void ConsensusContext_StateChanged(object? sender, ContextState e)
            {
                if (e.Step == consensusStep && e.Height == height)
                {
                    asyncAutoResetEvent.Set();
                }
            }
        }

        public static async Task<T> WaitUntilPublishedAsync<T>(
            ConsensusReactor consensusContext,
            int height,
            CancellationToken cancellationToken)
            where T : ConsensusMessage
        {
            T? consensusMessage = null;
            var asyncAutoResetEvent = new AsyncAutoResetEvent();
            consensusContext.MessagePublished += ConsensusContext_MessagePublished;
            try
            {
                await asyncAutoResetEvent.WaitAsync(cancellationToken);
                return consensusMessage!;
            }
            finally
            {
                consensusContext.MessagePublished -= ConsensusContext_MessagePublished;
            }

            void ConsensusContext_MessagePublished(
                object? sender, (int Height, ConsensusMessage Message) e)
            {
                if (e.Message is T { } message && e.Height == height)
                {
                    consensusMessage = message;
                    asyncAutoResetEvent.Set();
                }
            }
        }
    }
}
