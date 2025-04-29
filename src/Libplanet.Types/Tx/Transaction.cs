using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class Transaction
    : IEquatable<Transaction>, IComparable<Transaction>, IComparable, IValidatableObject
{
    private static readonly Codec Codec = new();
    private TxId? _id;

    [JsonIgnore]
    [Property(0)]
    public required UnsignedTx UnsignedTx { get; init; }

    [JsonIgnore]
    [Property(1)]
    [NonDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public TxId Id => _id ??= TxMarshaler.GetTxId(UnsignedTx, Signature);

    public long Nonce => UnsignedTx.Nonce;

    public Address Signer => UnsignedTx.Signer;

    public ImmutableSortedSet<Address> UpdatedAddresses => UnsignedTx.UpdatedAddresses;

    public ImmutableArray<IValue> Actions => UnsignedTx.Actions;

    public FungibleAssetValue? MaxGasPrice => UnsignedTx.MaxGasPrice;

    public long? GasLimit => UnsignedTx.GasLimit;

    public DateTimeOffset Timestamp => UnsignedTx.Timestamp;

    public BlockHash? GenesisHash => UnsignedTx.GenesisHash;

    public static Transaction Create(UnsignedTx unsignedTx, ImmutableArray<byte> signature)
    {
        return new Transaction
        {
            UnsignedTx = unsignedTx,
            Signature = signature,
        };
    }

    public static Transaction Create(UnsignedTx unsignedTx, PrivateKey privateKey)
        => Create(unsignedTx, [.. unsignedTx.CreateSignature(privateKey)]);


    // public static Transaction Deserialize(byte[] bytes)
    // {
    //     IValue value = new Codec().Decode(bytes);
    //     if (!(value is Bencodex.Types.Dictionary dict))
    //     {
    //         throw new DecodingException(
    //             $"Expected {typeof(Bencodex.Types.Dictionary)} but " +
    //             $"{value.GetType()}");
    //     }

    //     return TxMarshaler.UnmarshalTransaction(dict);
    // }

    public static Transaction Create(
        long nonce,
        PrivateKey privateKey,
        BlockHash genesisHash,
        IEnumerable<IValue> actions,
        FungibleAssetValue? maxGasPrice = null,
        long gasLimit = 0L,
        DateTimeOffset? timestamp = null)
    {
        if (privateKey is null)
        {
            throw new ArgumentNullException(nameof(privateKey));
        }

        var draftInvoice = new TxInvoice
        {
            Actions = [.. actions],
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

    // public byte[] Serialize() => Codec.Encode(TxMarshaler.MarshalTransaction(this));

    public bool Equals(Transaction? other) => Id.Equals(other?.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public int CompareTo(object? obj)
    {
        if (obj is not Transaction other)
        {
            throw new ArgumentException(
                $"Expected {nameof(Transaction)} but {obj?.GetType()}");
        }

        return CompareTo(other);
    }

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

    // internal static Transaction CombineWithoutVerification(
    //     UnsignedTx unsignedTx, ImmutableArray<byte> alreadyVerifiedSignature)
    //     => new Transaction(unsignedTx, alreadyVerifiedSignature);

    // private static Transaction Create(
    //     long nonce,
    //     PrivateKey privateKey,
    //     BlockHash? genesisHash,
    //     ImmutableArray<IValue> actions,
    //     FungibleAssetValue? maxGasPrice = null,
    //     long gasLimit = 0L,
    //     DateTimeOffset? timestamp = null)
    // {
    //     if (privateKey is null)
    //     {
    //         throw new ArgumentNullException(nameof(privateKey));
    //     }

    //     var draftInvoice = new TxInvoice
    //     {
    //         Actions = actions,
    //         GenesisHash = genesisHash ?? default,
    //         Timestamp = timestamp ?? DateTimeOffset.UtcNow,
    //         MaxGasPrice = maxGasPrice ?? default,
    //         GasLimit = gasLimit,
    //     };
    //     var signMeta = new TxSigningMetadata(privateKey.Address, nonce);
    //     var invoice = new TxInvoice
    //     {
    //         Actions = draftInvoice.Actions,
    //         GenesisHash = draftInvoice.GenesisHash,
    //         UpdatedAddresses = draftInvoice.UpdatedAddresses,
    //         Timestamp = draftInvoice.Timestamp,
    //         MaxGasPrice = draftInvoice.MaxGasPrice,
    //         GasLimit = draftInvoice.GasLimit,
    //     };
    //     var unsignedTx = new UnsignedTx(invoice, signMeta);
    //     return new Transaction(unsignedTx, privateKey);
    // }

    private static ImmutableArray<byte> ValidateSignature(
        UnsignedTx unsignedTx, ImmutableArray<byte> signature)
    {
        if (!unsignedTx.VerifySignature(signature))
        {
            throw new InvalidOperationException(
                "The given signature is not valid.");
        }

        return signature;
    }
}
