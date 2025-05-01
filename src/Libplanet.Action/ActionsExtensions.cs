using Bencodex.Types;
using Libplanet.Serialization;

namespace Libplanet.Action;

public static class ActionsExtensions
{
    public static ImmutableArray<IValue> ToPlainValues(this IEnumerable<IAction> actions)
        => [.. actions.Select(ModelSerializer.Serialize)];
}
