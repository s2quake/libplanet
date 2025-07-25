// using System.Threading;
// using System.Threading.Tasks;
// using Libplanet.Net.Services.Extensions;

// namespace Libplanet.Net.Tasks;

// internal sealed class BlockDemandPollTask(
//     ITransport transport, PeerService peerService, BlockDemandCollection blockDemands)
//     : BackgroundServiceBase
// {
//     internal BlockDemandPollTask(Swarm swarm)
//         : this(swarm.Transport, swarm.PeerService, swarm.BlockDemands)
//     {
//     }

//     protected override TimeSpan Interval => TimeSpan.FromMilliseconds(100);

//     protected override Task ExecuteAsync(CancellationToken cancellationToken)
//         => blockDemands.PollAsync(transport, [.. peerService.Peers], cancellationToken);
// }
