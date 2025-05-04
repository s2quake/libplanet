using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public static class ActionsExtensions
{
    public static IEnumerable<ActionBytecode> ToBytecodes(this IEnumerable<IAction> actions)
        => actions.Select(item => item.ToBytecode());

    public static ImmutableArray<IAction> FromImmutableBytes(this ImmutableArray<ImmutableArray<byte>> bytes)
        => [.. bytes.Select(ModelSerializer.DeserializeFromBytes<IAction>)];

    public static T ToAction<T>(this ActionBytecode actionBytecode)
        where T : IAction => ModelSerializer.DeserializeFromBytes<T>(actionBytecode.Bytes);

    // public static Transaction Create(
    //     this IEnumerable<IAction> actions,
    //     long nonce,
    //     PrivateKey privateKey,
    //     BlockHash genesisHash,
    //     FungibleAssetValue? maxGasPrice = null,
    //     long gasLimit = 0L,
    //     DateTimeOffset? timestamp = null)
    // {
    //     return Transaction.Create(
    //         nonce,
    //         privateKey,
    //         genesisHash,
    //         actions.ToImmutableBytes(),
    //         maxGasPrice,
    //         gasLimit,
    //         timestamp);
    // }

}
