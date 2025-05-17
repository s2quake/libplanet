using Libplanet.Serialization;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public static class ActionsExtensions
{
    public static ImmutableArray<ActionBytecode> ToBytecodes(this IEnumerable<IAction> actions)
        => [.. actions.Select(item => item.ToBytecode())];

    public static ImmutableArray<IAction> FromImmutableBytes(this ImmutableArray<ActionBytecode> bytecodes)
        => [.. bytecodes.Select(item => ModelSerializer.DeserializeFromBytes<IAction>(item.Bytes.AsSpan()))];

    public static T ToAction<T>(this ActionBytecode actionBytecode)
        where T : IAction => ModelSerializer.DeserializeFromBytes<T>(actionBytecode.Bytes.AsSpan());

    public static ActionBytecode ToBytecode(this IAction action)
        => new(ModelSerializer.SerializeToBytes(action));
}
