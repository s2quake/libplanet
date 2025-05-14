using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class Transaction
    : IEquatable<Transaction>, IComparable<Transaction>, IComparable, IValidatableObject, IHasKey<TxId>
{
    private TxId? _id;

    [JsonIgnore]
    [Property(0)]
    public required UnsignedTx UnsignedTx { get; init; }

    [JsonIgnore]
    [Property(1)]
    [NotDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public TxId Id => _id ??= new TxId(SHA256.HashData(ModelSerializer.SerializeToBytes(this)));

    public long Nonce => UnsignedTx.Nonce;

    public Address Signer => UnsignedTx.Signer;

    public ImmutableSortedSet<Address> UpdatedAddresses => UnsignedTx.UpdatedAddresses;

    public ImmutableArray<ActionBytecode> Actions => UnsignedTx.Actions;

    public FungibleAssetValue? MaxGasPrice => UnsignedTx.MaxGasPrice;

    public long? GasLimit => UnsignedTx.GasLimit;

    public DateTimeOffset Timestamp => UnsignedTx.Timestamp;

    public BlockHash? GenesisHash => UnsignedTx.GenesisHash;

    TxId IHasKey<TxId>.Key => Id;

    public static Transaction Create(UnsignedTx unsignedTx, ImmutableArray<byte> signature) => new()
    {
        UnsignedTx = unsignedTx,
        Signature = signature,
    };

    public static Transaction Create(UnsignedTx unsignedTx, PrivateKey privateKey)
        => Create(unsignedTx, [.. unsignedTx.CreateSignature(privateKey)]);

    public static Transaction Create(
        long nonce,
        PrivateKey privateKey,
        BlockHash genesisHash,
        ImmutableArray<ActionBytecode> actions,
        FungibleAssetValue? maxGasPrice = null,
        long gasLimit = 0L,
        DateTimeOffset? timestamp = null)
    {
        var draftInvoice = new TxInvoice
        {
            Actions = actions,
            GenesisHash = genesisHash,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            MaxGasPrice = maxGasPrice ?? default,
            GasLimit = gasLimit,
        };
        var signingMetadata = new TxSigningMetadata
        {
            Signer = privateKey.Address,
            Nonce = nonce,
        };
        var invoice = new TxInvoice
        {
            Actions = draftInvoice.Actions,
            GenesisHash = draftInvoice.GenesisHash,
            UpdatedAddresses = draftInvoice.UpdatedAddresses,
            Timestamp = draftInvoice.Timestamp,
            MaxGasPrice = draftInvoice.MaxGasPrice,
            GasLimit = draftInvoice.GasLimit,
        };
        var unsignedTx = new UnsignedTx
        {
            Invoice = invoice,
            SigningMetadata = signingMetadata,
        };
        return Create(unsignedTx, privateKey);
    }

    public bool Equals(Transaction? other) => Id.Equals(other?.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        Transaction other => CompareTo(other),
        _ => throw new ArgumentException($"Argument {nameof(obj)} is not ${nameof(Transaction)}.", nameof(obj)),
    };

    public int CompareTo(Transaction? other)
    {
        if (other is null)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (!UnsignedTx.VerifySignature(Signature))
        {
            yield return new ValidationResult(
                "The given signature is not valid.",
                [nameof(Signature)]);
        }
    }
}
