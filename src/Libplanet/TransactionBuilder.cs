using Libplanet.State;
using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;
using Libplanet.Types.Blocks;

namespace Libplanet;

public sealed record class TransactionBuilder
{
    public required PrivateKey Signer { get; init; }

    public long Nonce { get; init; }

    public BlockHash GenesisHash { get; init; }

    public IAction[] Actions { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public FungibleAssetValue? MaxGasPrice { get; init; }

    public long GasLimit { get; init; }

    public Transaction Create()
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = false,
        };
        var metadata = new TransactionMetadata
        {
            Nonce = Nonce,
            Signer = Signer.Address,
            GenesisHash = GenesisHash,
            Actions = Actions.ToBytecodes(),
            Timestamp = Timestamp,
            MaxGasPrice = MaxGasPrice,
            GasLimit = GasLimit,
        };
        var bytes = ModelSerializer.SerializeToBytes(metadata, options);
        var signature = Signer.Sign(bytes).ToImmutableArray();

        return new Transaction
        {
            Metadata = metadata,
            Signature = signature,
        };
    }
}
