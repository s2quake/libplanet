// using System.Security.Cryptography;
// using Libplanet.Types;

// namespace Libplanet.Action;

// public sealed record class CommittedActionEvaluation
// {
//     public required IAction Action { get; init; }

//     public required IActionContext InputContext { get; init; }

//     public HashDigest<SHA256> InputState { get; init; }

//     public HashDigest<SHA256> OutputState { get; init; }

//     public Exception? Exception { get; init; }

//     public static explicit operator CommittedActionEvaluation(ActionEvaluation evaluation) => new()
//     {
//         Action = evaluation.Action,
//         InputContext = evaluation.InputContext,
//         // InputContext = new CommittedActionContext
//         // {
//         //     Signer = evaluation.InputContext.Signer,
//         //     TxId = evaluation.InputContext.TxId,
//         //     Proposer = evaluation.InputContext.Proposer,
//         //     BlockHeight = evaluation.InputContext.BlockHeight,
//         //     BlockProtocolVersion = evaluation.InputContext.BlockProtocolVersion,
//         //     PreviousState = evaluation.InputWorld.Trie.IsCommitted
//         //                 ? evaluation.InputWorld.Trie.Hash
//         //                 : throw new ArgumentException("Trie is not recorded"),
//         //     RandomSeed = evaluation.InputContext.RandomSeed,
//         // },
//         InputState = evaluation.InputWorld.Trie.Hash,
//         OutputState = evaluation.OutputWorld.Trie.Hash,
//         Exception = evaluation.Exception,
//     };
// }
