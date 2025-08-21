using Libplanet.State;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet;

public sealed record class InitialTransactionBuilder
{
    public long Nonce { get; init; }

    public IAction[] Actions { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public FungibleAssetValue? MaxGasPrice { get; init; }

    public long GasLimit { get; init; }

    public Transaction Create(ISigner signer)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var metadata = new TransactionMetadata
        {
            Nonce = Nonce,
            Signer = signer.Address,
            GenesisBlockHash = default,
            Actions = Actions.ToBytecodes(),
            Timestamp = Timestamp,
            MaxGasPrice = MaxGasPrice,
            GasLimit = GasLimit,
        };
        var bytes = ModelSerializer.SerializeToBytes(metadata, options);
        var signature = signer.Sign(bytes).ToImmutableArray();

        return new Transaction
        {
            Metadata = metadata,
            Signature = signature,
        };
    }
}
