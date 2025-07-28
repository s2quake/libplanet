// using System.Threading.Tasks;
// using Libplanet.Types;
// using Libplanet.Tests;
// using System.Threading;
// using Libplanet.TestUtilities;
// using System.Reactive.Subjects;

// namespace Libplanet.Net.Tests;

// public sealed class TestMiningService(Random random, Blockchain blockchain, PrivateKey privateKey)
//     : BackgroundServiceBase
// {
//     private readonly Subject<(Block, BlockCommit)> _blockAppendedSubject = new();

//     public TestMiningService(Blockchain blockchain, PrivateKey privateKey)
//         : this(Random.Shared, blockchain, privateKey)
//     {
//     }

//     public IObservable<(Block Block, BlockCommit BlockCommit)> BlockAppended => _blockAppendedSubject;

//     public TimeSpan MinimumInterval { get; set; } = TimeSpan.FromMilliseconds(100);

//     public TimeSpan MaximumInterval { get; set; } = TimeSpan.FromSeconds(2);

//     public Func<Blockchain, bool> Predicate { get; set; } = _ => true;

//     protected override async Task ExecuteAsync(CancellationToken cancellationToken)
//     {
//         var item = blockchain.ProposeAndAppend(privateKey);
//         _blockAppendedSubject.OnNext(item);
//         await Task.CompletedTask;
//     }

//     protected override async ValueTask DisposeAsyncCore()
//     {
//         _blockAppendedSubject.Dispose();
//         await base.DisposeAsyncCore();
//     }

//     protected override TimeSpan GetInterval()
//         => RandomUtility.TimeSpan(random, MinimumInterval.Milliseconds, MaximumInterval.Milliseconds);
// }
