using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class Transaction(UnsignedTx UnsignedTx, ImmutableArray<byte> Signature)
    : IEquatable<Transaction>, IComparable<Transaction>, IComparable
{
    private static readonly Codec Codec = new();
    private TxId? _id;

    public Transaction(UnsignedTx unsignedTx, PrivateKey privateKey)
        : this(unsignedTx, [.. unsignedTx.CreateSignature(privateKey)])
    {
    }

    [JsonIgnore]
    [Property(0)]
    public UnsignedTx UnsignedTx { get; } = UnsignedTx;

    [JsonIgnore]
    [Property(1)]
    public ImmutableArray<byte> Signature { get; } = ValidateSignature(UnsignedTx, Signature);

    public TxId Id => _id ??= TxMarshaler.GetTxId(UnsignedTx, Signature);

    public long Nonce => UnsignedTx.Nonce;

    public Address Signer => UnsignedTx.Signer;

    public ImmutableSortedSet<Address> UpdatedAddresses => UnsignedTx.UpdatedAddresses;

    public ImmutableArray<IValue> Actions => UnsignedTx.Actions;

    public FungibleAssetValue? MaxGasPrice => UnsignedTx.MaxGasPrice;

    public long? GasLimit => UnsignedTx.GasLimit;

    public DateTimeOffset Timestamp => UnsignedTx.Timestamp;

    public BlockHash? GenesisHash => UnsignedTx.GenesisHash;

    public static Transaction Deserialize(byte[] bytes)
    {
        IValue value = new Codec().Decode(bytes);
        if (!(value is Bencodex.Types.Dictionary dict))
        {
            throw new DecodingException(
                $"Expected {typeof(Bencodex.Types.Dictionary)} but " +
                $"{value.GetType()}");
        }

        return TxMarshaler.UnmarshalTransaction(dict);
    }

    public static Transaction Create(
        long nonce,
        PrivateKey privateKey,
        BlockHash? genesisHash,
        IEnumerable<IValue> actions,
        FungibleAssetValue? maxGasPrice = null,
        long? gasLimit = null,
        DateTimeOffset? timestamp = null) =>
        Create(
            nonce,
            privateKey,
            genesisHash,
            [.. actions],
            maxGasPrice,
            gasLimit,
            timestamp);

    public byte[] Serialize() => Codec.Encode(TxMarshaler.MarshalTransaction(this));

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

    internal static Transaction CombineWithoutVerification(
        UnsignedTx unsignedTx, ImmutableArray<byte> alreadyVerifiedSignature)
        => new Transaction(unsignedTx, alreadyVerifiedSignature);

    private static Transaction Create(
        long nonce,
        PrivateKey privateKey,
        BlockHash? genesisHash,
        ImmutableArray<IValue> actions,
        FungibleAssetValue? maxGasPrice = null,
        long? gasLimit = null,
        DateTimeOffset? timestamp = null)
    {
        if (privateKey is null)
        {
            throw new ArgumentNullException(nameof(privateKey));
        }

        var draftInvoice = new TxInvoice
        {
            Actions = actions,
            GenesisHash = genesisHash,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            MaxGasPrice = maxGasPrice,
            GasLimit = gasLimit,
        };
        var signMeta = new TxSigningMetadata(privateKey.Address, nonce);
        var invoice = new TxInvoice
        {
            Actions = draftInvoice.Actions,
            GenesisHash = draftInvoice.GenesisHash,
            UpdatedAddresses = draftInvoice.UpdatedAddresses,
            Timestamp = draftInvoice.Timestamp,
            MaxGasPrice = draftInvoice.MaxGasPrice,
            GasLimit = draftInvoice.GasLimit,
        };
        var unsignedTx = new UnsignedTx(invoice, signMeta);
        return new Transaction(unsignedTx, privateKey);
    }

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
