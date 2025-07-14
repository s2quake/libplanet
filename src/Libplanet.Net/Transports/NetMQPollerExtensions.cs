using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;

namespace Libplanet.Net.Transports;

internal static class NetMQPollerExtensions
{
    public static async Task StartAsync(this NetMQPoller @this, CancellationToken cancellationToken)
    {
        @this.RunAsync();
        while (!@this.IsRunning)
        {
            await Task.Yield();
        }
    }

    public static async Task StopAsync(this NetMQPoller @this, CancellationToken cancellationToken)
    {
        @this.StopAsync();
        while (@this.IsRunning)
        {
            await Task.Yield();
        }

        @this.Dispose();
    }

    public static async ValueTask DisposeAsync(this NetMQPoller @this)
    {
        @this.StopAsync();
        while (@this.IsRunning)
        {
            await Task.Yield();
        }

        @this.Dispose();
    }
}
