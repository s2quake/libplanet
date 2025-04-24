using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public sealed record class Transaction(UnsignedTx UnsignedTx, ImmutableArray<byte> Signature)
    : IEquatable<Transaction>,
    IEquatable<TxInvoice>,
    IEquatable<TxSigningMetadata>,
    IEquatable<UnsignedTx>
{
    private static readonly Codec Codec = new();
    private TxId? _id;

    public Transaction(UnsignedTx unsignedTx, PrivateKey privateKey)
        : this(unsignedTx, [.. unsignedTx.CreateSignature(privateKey)])
    {
    }

    [JsonIgnore]
    public UnsignedTx UnsignedTx { get; } = UnsignedTx;

    [JsonIgnore]
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

    bool IEquatable<TxInvoice>.Equals(TxInvoice? other) =>
        other is { } o && o.Equals(UnsignedTx);

    bool IEquatable<TxSigningMetadata>.Equals(TxSigningMetadata? other) =>
        other is { } o && o.Equals(UnsignedTx);

    bool IEquatable<UnsignedTx>.Equals(UnsignedTx? other) =>
        other is { } o && o.Equals(UnsignedTx);

    public bool Equals(Transaction? other) => Id.Equals(other?.Id);

    public override int GetHashCode() => Id.GetHashCode();

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
            throw new InvalidTxSignatureException(
                "The given signature is not valid.", TxMarshaler.GetTxId(unsignedTx, signature));
        }

        return signature;
    }
}
