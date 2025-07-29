// using System.Reactive.Subjects;
// using System.Threading;
// using System.Threading.Tasks;
// using System.Transactions;
// using Libplanet.Types.Threading;

// namespace Libplanet.Net.Components;

// public sealed class TransactionAppender(Blockchain blockchain) : IDisposable
// {
//     private readonly Subject<ImmutableArray<Transaction>> _appendedSubject = new();
//     private readonly Subject<(ImmutableArray<Transaction>, Exception)> _appendFailedSubject = new();

//     public IObservable<ImmutableArray<Transaction>> Appended => _appendedSubject;

//     public IObservable<(ImmutableArray<Transaction>, Exception)> AppendFailed => _appendFailedSubject;

//     public void Dispose()
//     {
//         _appendedSubject.Dispose();
//         _appendFailedSubject.Dispose();
//     }

//     public async Task AppendAsync(ImmutableArray<Transaction>Collection blockBranches, CancellationToken cancellationToken)
//     {
//         var taskList = new List<Task>(blockBranches.Count);
//         foreach (var blockBranch in blockBranches.Flush(blockchain))
//         {
//             taskList.Add(AppendBranchAsync(blockBranch, cancellationToken));
//         }

//         await TaskUtility.TryWhenAll(taskList);
//     }

//     private async Task AppendBranchAsync(ImmutableArray<Transaction> blockBranch, CancellationToken cancellationToken)
//     {
//         try
//         {
//             for (var i = 0; i < blockBranch.Blocks.Length; i++)
//             {
//                 cancellationToken.ThrowIfCancellationRequested();
//                 blockchain.Append(blockBranch.Blocks[i], blockBranch.BlockCommits[i]);
//                 await Task.Yield();
//             }

//             _appendedSubject.OnNext(blockBranch);
//         }
//         catch (Exception e) when (e is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
//         {
//             _blockBranchAppendFailedSubject.OnNext((blockBranch, e));
//         }
//     }
// }
