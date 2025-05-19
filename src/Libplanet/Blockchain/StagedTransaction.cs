// using Libplanet.Types.Tx;

// namespace Libplanet.Blockchain;

// public sealed record class StagedTransaction
// {
//     public required Transaction Transaction { get; init; }

//     public required DateTimeOffset Lifetime { get; init; }

//     public bool IsIgnored { get; init; }

//     public bool IsExpired => Lifetime < DateTimeOffset.UtcNow;

//     public bool IsEnabled(Libplanet.Store.Repository store, Guid blockChainId)
//     {
//         if (IsExpired)
//         {
//             return false;
//         }

//         if (store.Chains.GetOrAdd(blockChainId).Nonces[Transaction.Signer] < Transaction.Nonce)
//         {
//             return false;
//         }

//         return !IsIgnored;
//     }
// }
