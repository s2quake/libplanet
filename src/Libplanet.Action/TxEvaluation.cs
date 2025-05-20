using System.Transactions;
using Libplanet.Action.State;

namespace Libplanet.Action;

public sealed record class TxEvaluation
{
    public required Transaction Transaction { get; init; }

    public required World InputWorld { get; init; }

    public required World OutputWorld { get; init; }

    public ActionEvaluation[] Evaluations { get; init; } = [];
}
