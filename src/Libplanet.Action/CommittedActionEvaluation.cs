using System.Security.Cryptography;
using Libplanet.Common;

namespace Libplanet.Action;

public sealed record class CommittedActionEvaluation
{
    public required IAction Action { get; init; }

    public required CommittedActionContext InputContext { get; init; }

    public HashDigest<SHA256> OutputState { get; init; }

    public Exception? Exception { get; init; }
}
