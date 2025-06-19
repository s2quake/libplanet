using Libplanet.State;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet;

public sealed record class TransactionBuilder
{
    public long Nonce { get; init; }

    public BlockHash GenesisHash { get; init; }

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
            GenesisHash = GenesisHash,
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

    public Transaction Create(ISigner signer, Blockchain blockchain)
    {
        var builder = this with
        {
            Nonce = blockchain.GetNextTxNonce(signer.Address),
            GenesisHash = blockchain.Genesis.BlockHash,
        };
        return builder.Create(signer);
    }
}
