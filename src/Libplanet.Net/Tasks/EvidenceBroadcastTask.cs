// using System.Threading;
// using System.Threading.Tasks;
// using Libplanet.Net.Messages;

// namespace Libplanet.Net.Tasks;

// internal sealed class EvidenceBroadcastTask(Swarm swarm) : PeriodicTaskService
// {
//     protected override TimeSpan GetInterval()
//     {
//         return swarm.Options.EvidenceBroadcastInterval;
//     }

//     protected override async Task ExecuteAsync(CancellationToken cancellationToken)
//     {
//         var blockchain = swarm.Blockchain;
//         var evidenceIds = blockchain.PendingEvidence.Keys.ToArray();
//         if (evidenceIds.Length > 0)
//         {
//             var message = new EvidenceIdMessage { Ids = [.. evidenceIds] };
//             swarm.BroadcastMessage(default, message);
//         }

//         await Task.CompletedTask;
//     }
// }
