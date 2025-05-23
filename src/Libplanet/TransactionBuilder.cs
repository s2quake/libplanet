using Libplanet.State;
using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet;

public sealed record class TransactionBuilder
{
    public required PrivateKey Signer { get; init; }

    public required Blockchain Blockchain { get; init; }

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
            Nonce = Blockchain.GetNextTxNonce(Signer.Address),
            Signer = Signer.Address,
            GenesisHash = Blockchain.Genesis.BlockHash,
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
