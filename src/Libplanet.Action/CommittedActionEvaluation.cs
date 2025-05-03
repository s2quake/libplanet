using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Action;

public sealed record class CommittedActionEvaluation
{
    public required IAction Action { get; init; }

    public required CommittedActionContext InputContext { get; init; }

    public HashDigest<SHA256> OutputState { get; init; }

    public Exception? Exception { get; init; }

    public static explicit operator CommittedActionEvaluation(ActionEvaluation evaluation)
    {
        return new CommittedActionEvaluation
        {
            Action = evaluation.Action,
            InputContext = new CommittedActionContext
            {
                Signer = evaluation.InputContext.Signer,
                TxId = evaluation.InputContext.TxId,
                Proposer = evaluation.InputContext.Proposer,
                BlockHeight = evaluation.InputContext.BlockHeight,
                BlockProtocolVersion = evaluation.InputContext.BlockProtocolVersion,
                PreviousState = evaluation.InputWorld.Trie.IsCommitted
                        ? evaluation.InputWorld.Trie.Hash
                        : throw new ArgumentException("Trie is not recorded"),
                RandomSeed = evaluation.InputContext.RandomSeed,
            },
            OutputState = evaluation.OutputWorld.Trie.IsCommitted
                    ? evaluation.OutputWorld.Trie.Hash
                    : throw new ArgumentException("Trie is not recorded"),
            Exception = evaluation.Exception,
        };
    }
}
